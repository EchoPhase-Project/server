// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Queries;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class UserPermissionGrantRepository : RepositoryBase<UserPermissionGrant>
    {
        private readonly PostgresContext _context;

        public UserPermissionGrantRepository(PostgresContext context)
        {
            _context = context;
        }

        public new UserPermissionGrantQuery Query() => new(Build());

        public override IQueryable<UserPermissionGrant> Build()
            => _context.UserPermissionGrants;

        public override void Add(UserPermissionGrant entity) => _context.UserPermissionGrants.Add(entity);
        public override void Update(UserPermissionGrant entity) => _context.UserPermissionGrants.Update(entity);
        public override void Remove(UserPermissionGrant entity) => _context.UserPermissionGrants.Remove(entity);
        public override Task<int> SaveAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
