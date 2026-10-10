// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Authentication;
using EchoPhase.Configuration.Authentication.Impersonation;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;
using EchoPhase.Security.Authentication.Jwt.Claims;
using EchoPhase.Security.Authentication.Jwt.Providers;
using EchoPhase.Security.BitMasks;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Security.Hashers;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ImpersonationEntity = EchoPhase.DAL.Postgres.Models.Impersonation;

namespace EchoPhase.Security.Authentication.Impersonation
{
    public class ImpersonationService : IImpersonationService
    {
        private const int MaxReasonLength = 256;
        private const int ListLimit = 50;

        private readonly ImpersonationRepository _repository;

        private readonly UserRepository _users;
        private readonly UserManager<User> _userManager;
        private readonly IRoleService _roles;
        private readonly IJwtTokenProvider _jwt;
        private readonly IImpersonationEvents _events;
        private readonly ImpersonationOptions _settings;
        private readonly ILogger<ImpersonationService> _logger;
        private readonly PasswordHashingGate _hashing;
        private readonly TimeProvider _time;

        public ImpersonationService(
            ImpersonationRepository repository,
            UserRepository users,
            UserManager<User> userManager,
            PasswordHashingGate hashing,
            IRoleService roles,
            IJwtTokenProvider jwt,
            IImpersonationEvents events,
            IOptions<AuthenticationOptions> settings,
            ILogger<ImpersonationService> logger,
            TimeProvider time)
        {
            _repository = repository;
            _users = users;
            _userManager = userManager;
            _hashing = hashing;
            _roles = roles;
            _jwt = jwt;
            _events = events;
            _settings = settings.Value.Impersonation;
            _logger = logger;
            _time = time;
        }

        public async Task<IServiceResult<ImpersonationStarted>> StartAsync(
            Guid actorId, string targetUserName, string reason, string password, CancellationToken ct = default)
        {
            reason = reason.Trim();
            if (reason.Length == 0 || reason.Length > MaxReasonLength)
                return Failure<ImpersonationStarted>(ImpersonationErrors.Invalid,
                    $"A reason of 1-{MaxReasonLength} characters is required.");

            var actor = await _users.Query().WithIds(actorId).FirstOrDefaultAsync(ct);
            if (actor is null)
                return Failure<ImpersonationStarted>(ImpersonationErrors.NotFound, "User not found.");

            // Re-confirming the password keeps a stolen session from being
            // turned into access to other accounts. Failures count towards lockout.
            if (await _userManager.IsLockedOutAsync(actor) || !await _hashing.RunAsync(() => _userManager.CheckPasswordAsync(actor, password), ct))
            {
                await _userManager.AccessFailedAsync(actor);
                return Failure<ImpersonationStarted>(ImpersonationErrors.InvalidCredentials, "Invalid password.");
            }

            await _userManager.ResetAccessFailedCountAsync(actor);

            var target = await _users.Query().WithUserNames(targetUserName).FirstOrDefaultAsync(ct);
            if (target is null)
                return Failure<ImpersonationStarted>(ImpersonationErrors.NotFound, "User not found.");

            if (target.Id == actor.Id)
                return Failure<ImpersonationStarted>(ImpersonationErrors.Invalid, "You cannot impersonate yourself.");

            var actorRoles = (await _roles.GetRolesAsync(actor)).ToArray();
            var targetRoles = (await _roles.GetRolesAsync(target)).ToArray();

            // Roles alone are enough: impersonation is role-only, never a per-user grant.
            var ceiling = RolePermissions.CeilingFor(actorRoles);
            if (!ceiling.TryGetValue(Resources.Users, out var userActions)
                || !userActions.Contains(Permissions.Impersonate))
                return Failure<ImpersonationStarted>(ImpersonationErrors.Forbidden, "Not allowed to impersonate.");

            // Otherwise an admin could act as another admin and hide behind them.
            if (RolePermissions.Rank(actorRoles) <= RolePermissions.Rank(targetRoles))
                return Failure<ImpersonationStarted>(ImpersonationErrors.Forbidden,
                    "You can only impersonate users with lower roles than yours.");

            var impersonation = new ImpersonationEntity
            {
                ActorId = actor.Id,
                TargetId = target.Id,
                Reason = reason,
                ActorSecurityStamp = actor.SecurityStamp ?? string.Empty,
                ExpiresAt = _time.GetUtcNow().UtcDateTime + _settings.TokenLifetime
            };

            _repository.Add(impersonation);
            await _repository.SaveAsync(ct);

            // The target's full ceiling; credential management is closed to
            // impersonation tokens by [RequirePrincipal(Session)].
            var token = await _jwt.GenerateTokenAsync(target, _settings.TokenLifetime, new ClaimsEnrichmentContext
            {
                User = target,
                ActorId = actor.Id,
                ImpersonationId = impersonation.Id
            });

            _logger.LogWarning(
                "User {ActorId} started impersonating {TargetId} ({ImpersonationId}): {Reason}",
                actor.Id, target.Id, impersonation.Id, reason);

            try
            {
                await _events.StartedAsync(target.Id, new ImpersonationNotice(
                    impersonation.Id, actor.Id, actor.UserName, reason, impersonation.ExpiresAt), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify user {TargetId} about impersonation {ImpersonationId}.",
                    target.Id, impersonation.Id);
            }

            return ServiceResult<ImpersonationStarted>.Success(new ImpersonationStarted(
                impersonation.Id, target.Id, target.UserName, token, (int)_settings.TokenLifetime.TotalSeconds));
        }

        public async Task<IServiceResult> EndAsync(Guid actorId, Guid impersonationId, CancellationToken ct = default)
            => await EndCoreAsync(await _repository.Query()
                .WithIds(impersonationId)
                .WithActorIds(actorId)
                .Active(_time.GetUtcNow().UtcDateTime)
                .FirstOrDefaultAsync(ct), ct);

        public async Task<IServiceResult> EndCurrentAsync(Guid targetId, Guid impersonationId, CancellationToken ct = default)
            => await EndCoreAsync(await _repository.Query()
                .WithIds(impersonationId)
                .WithTargetIds(targetId)
                .Active(_time.GetUtcNow().UtcDateTime)
                .FirstOrDefaultAsync(ct), ct);

        public async Task<IReadOnlyList<ImpersonationEntity>> ListAsync(Guid userId, CancellationToken ct = default)
        {
            return await _repository.Query()
                .Involving(userId)
                .OrderByDescending(i => i.CreatedAt)
                .Take(ListLimit)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        private async Task<IServiceResult> EndCoreAsync(ImpersonationEntity? impersonation, CancellationToken ct)
        {
            if (impersonation is null)
                return ServiceResult.Failure(e => e.Set(ImpersonationErrors.NotFound, "Active impersonation not found."));

            impersonation.EndedAt = _time.GetUtcNow().UtcDateTime;
            await _repository.SaveAsync(ct);

            _logger.LogInformation("Impersonation {ImpersonationId} ended.", impersonation.Id);
            return ServiceResult.Success();
        }

        private static IServiceResult<T> Failure<T>(string code, string message)
            => ServiceResult<T>.Failure(e => e.Set(code, message));
    }
}
