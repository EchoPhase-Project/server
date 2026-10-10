// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Tests
{
    /// <summary>
    /// Translation only: building SQL needs the Npgsql provider, not a server.
    /// </summary>
    public class WebHookQueryTests : IDisposable
    {
        private readonly PostgresContext _context = new(new DbContextOptionsBuilder<PostgresContext>()
            .UseNpgsql("Host=localhost;Database=none")
            .Options);

        public void Dispose() => _context.Dispose();

        [Fact]
        public void WithIntents_FiltersInSqlWithArrayContainment()
        {
            var sql = new WebHookRepository(_context).Query()
                .WithStatus(WebHookStatus.Enabled)
                .WithIntents("blocks", "connections")
                .ToQueryString();

            // "required <@ column" is the commutator of "column @> required"; both use the GIN index.
            Assert.True(sql.Contains("@intents <@ w.\"Intents\""), sql);
        }

        [Fact]
        public void WithIntents_StaysAnEfQuery()
        {
            // An in-memory filter would make ToQueryString throw.
            var sql = new WebHookRepository(_context).Query()
                .WithUserIds(Guid.NewGuid())
                .WithIntents("blocks")
                .ToQueryString();

            Assert.Contains("FROM \"EchoPhase\".\"WebHooks\"", sql);
        }
    }
}
