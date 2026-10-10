// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using Microsoft.AspNetCore.Identity;

namespace EchoPhase.Services.Permissions
{
    /// <summary>
    /// An expired grant stops counting as soon as a token is issued, but tokens
    /// issued before still carry it. Once a minute this revokes the tokens of
    /// users whose grants expired, through the same security stamp change as
    /// any other permission change.
    /// </summary>
    public class PermissionGrantExpiryWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<PermissionGrantExpiryWorker> _logger;
        private readonly TimeProvider _time;

        public PermissionGrantExpiryWorker(IServiceScopeFactory scopes, ILogger<PermissionGrantExpiryWorker> logger, TimeProvider time)
        {
            _scopes = scopes;
            _logger = logger;
            _time = time;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            do
            {
                try
                {
                    await ApplyExpiriesAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Retried on the next tick; nothing is marked applied on failure.
                    _logger.LogError(ex, "Applying permission grant expiries failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task ApplyExpiriesAsync(CancellationToken ct)
        {
            await using var scope = _scopes.CreateAsyncScope();
            var grants = scope.ServiceProvider.GetRequiredService<UserPermissionGrantRepository>();
            var users = scope.ServiceProvider.GetRequiredService<UserRepository>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var due = await grants.Query().ExpiryDue(_time.GetUtcNow().UtcDateTime).ToListAsync(ct);

            foreach (var group in due.GroupBy(g => g.UserId))
            {
                foreach (var grant in group)
                    grant.ExpiryApplied = true;

                var user = await users.Query().WithIds(group.Key).FirstOrDefaultAsync(ct);
                if (user is null)
                {
                    await grants.SaveAsync(ct);
                    continue;
                }

                // Saves the flags together with the new stamp (shared context).
                var result = await userManager.UpdateSecurityStampAsync(user);
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Could not update the security stamp of {user.Id}: {string.Join("; ", result.Errors.Select(e => e.Description))}");

                _logger.LogInformation("Permission grants expired for user {UserId}: {Resources}; tokens revoked.",
                    user.Id, string.Join(", ", group.Select(g => g.Resource)));
            }
        }
    }
}
