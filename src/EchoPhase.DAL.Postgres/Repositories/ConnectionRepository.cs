// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Queries;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Repositories
{
    /// <summary>
    /// Stores secrets exactly as given; encryption is the caller's concern so
    /// tracked entities never hold plain text.
    /// </summary>
    public class ConnectionRepository : RepositoryBase<Connection>
    {
        private readonly PostgresContext _context;

        public ConnectionRepository(PostgresContext context)
        {
            _context = context;
        }

        public new ConnectionQuery Query() => new(Build());

        public override IQueryable<Connection> Build()
            => _context.Connections;

        public override void Add(Connection entity) => _context.Connections.Add(entity);
        public override void Update(Connection entity) => _context.Connections.Update(entity);
        public override void Remove(Connection entity) => _context.Connections.Remove(entity);
        public override Task<int> SaveAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);

        /// <summary>
        /// Stops tracking an entity so a later, fresher copy with the same key
        /// can be attached via <see cref="Update"/>.
        /// </summary>
        public void Detach(Connection entity)
            => _context.Entry(entity).State = EntityState.Detached;
    }
}
