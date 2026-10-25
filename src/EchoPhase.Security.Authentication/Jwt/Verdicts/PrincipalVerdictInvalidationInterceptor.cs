// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.CompilerServices;
using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ImpersonationEntity = EchoPhase.DAL.Postgres.Models.Impersonation;

namespace EchoPhase.Security.Authentication.Jwt.Verdicts
{
    /// <summary>
    /// Drops cached verdicts whenever something they depend on is saved, so no
    /// call site has to remember to: users (security stamp, deletion), API
    /// clients, impersonations and permission grants. Over-invalidating only costs a database check.
    /// </summary>
    public class PrincipalVerdictInvalidationInterceptor : SaveChangesInterceptor
    {
        private readonly IPrincipalVerdictCache _cache;
        private readonly ConditionalWeakTable<DbContext, Pending> _pending = new();
        private readonly TimeProvider _time;

        public PrincipalVerdictInvalidationInterceptor(IPrincipalVerdictCache cache, TimeProvider time)
        {
            _cache = cache;
            _time = time;
        }

        private sealed class Pending
        {
            public HashSet<Guid> Users { get; } = [];
            public HashSet<Guid> Actors { get; } = [];
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            Collect(eventData.Context);
            return base.SavingChangesAsync(eventData, result, ct);
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Collect(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        {
            if (eventData.Context is { } context && Take(context) is { } pending)
            {
                pending.Users.UnionWith(await ActiveTargetsAsync(context, pending.Actors, ct));
                await _cache.InvalidateAsync(pending.Users);
            }

            return await base.SavedChangesAsync(eventData, result, ct);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            if (eventData.Context is { } context && Take(context) is { } pending)
            {
                pending.Users.UnionWith(ActiveTargetsAsync(context, pending.Actors, default).GetAwaiter().GetResult());
                _cache.InvalidateAsync(pending.Users).GetAwaiter().GetResult();
            }

            return base.SavedChanges(eventData, result);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            Take(eventData.Context);
            base.SaveChangesFailed(eventData);
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken ct = default)
        {
            Take(eventData.Context);
            return base.SaveChangesFailedAsync(eventData, ct);
        }

        private void Collect(DbContext? context)
        {
            if (context is null)
                return;

            var pending = _pending.GetOrCreateValue(context);

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.State is not (EntityState.Modified or EntityState.Deleted))
                    continue;

                switch (entry.Entity)
                {
                    case User user:
                        pending.Users.Add(user.Id);
                        // An impersonation token dies with its actor's stamp.
                        pending.Actors.Add(user.Id);
                        break;
                    case ApiClient client:
                        pending.Users.Add(client.UserId);
                        break;
                    case ImpersonationEntity impersonation:
                        pending.Users.Add(impersonation.TargetId);
                        break;
                    // Grant services also change the stamp; this covers direct edits.
                    case UserPermissionGrant grant:
                        pending.Users.Add(grant.UserId);
                        break;
                }
            }
        }

        private Pending? Take(DbContext? context)
        {
            if (context is null || !_pending.TryGetValue(context, out var pending))
                return null;

            _pending.Remove(context);
            return pending.Users.Count > 0 ? pending : null;
        }

        private async Task<List<Guid>> ActiveTargetsAsync(
            DbContext context, HashSet<Guid> actors, CancellationToken ct)
        {
            if (actors.Count == 0)
                return [];

            var now = _time.GetUtcNow().UtcDateTime;
            return await context.Set<ImpersonationEntity>()
                .AsNoTracking()
                .Where(i => actors.Contains(i.ActorId) && i.EndedAt == null && i.ExpiresAt > now)
                .Select(i => i.TargetId)
                .ToListAsync(ct);
        }
    }
}
