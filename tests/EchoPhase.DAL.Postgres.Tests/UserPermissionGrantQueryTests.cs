// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Tests
{
    public class UserPermissionGrantQueryTests : IDisposable
    {
        private readonly PostgresContext _context = new(new DbContextOptionsBuilder<PostgresContext>()
            .UseNpgsql("Host=localhost;Database=none")
            .Options);

        public void Dispose() => _context.Dispose();

        [Fact]
        public void Active_KeepsOpenEndedAndUnexpiredGrants()
        {
            var sql = new UserPermissionGrantRepository(_context).Query()
                .WithUserIds(Guid.NewGuid())
                .Active(DateTime.UtcNow)
                .ToQueryString();

            Assert.True(sql.Contains("u.\"ExpiresAt\" IS NULL OR u.\"ExpiresAt\" > @now"), sql);
        }

        [Fact]
        public void ExpiryDue_MatchesThePartialIndexFilter()
        {
            var sql = new UserPermissionGrantRepository(_context).Query()
                .ExpiryDue(DateTime.UtcNow)
                .ToQueryString();

            Assert.True(sql.Contains("u.\"ExpiresAt\" IS NOT NULL"), sql);
            Assert.True(sql.Contains("NOT (u.\"ExpiryApplied\")"), sql);
        }
    }
}
