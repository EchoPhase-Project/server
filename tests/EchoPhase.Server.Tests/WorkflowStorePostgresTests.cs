// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Runner;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Runners.Blocks.Storage;
using EchoPhase.Server.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoPhase.Server.Tests
{
    /// <summary>
    /// WorkflowStore on a real Postgres: the advisory lock and the unique
    /// index (nulls not distinct). Only with ECHOPHASE_TEST_POSTGRES set to a
    /// connection string of a database it may migrate; skipped otherwise.
    /// It creates a user of its own and deletes it (its values go with it).
    /// </summary>
    public class WorkflowStorePostgresTests
    {
        private static readonly string? Connection = Environment.GetEnvironmentVariable("ECHOPHASE_TEST_POSTGRES");

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static PostgresContext Context() => new(new DbContextOptionsBuilder<PostgresContext>()
            .UseNpgsql(Connection, PostgresContext.ConfigureNpgsql)
            .Options);

        private static WorkflowStore Store(PostgresContext db) => new(db, Options.Create(new RunnerOptions()), TimeProvider.System);

        [Fact]
        public async Task ConcurrentUpdatesLoseNothing()
        {
            Assert.SkipWhen(Connection is null, "ECHOPHASE_TEST_POSTGRES is not set.");
            var userId = await CreateUserAsync();
            try
            {
                var slot = new StorageSlot(userId, null);
                // Each "run" its own context and connection, as in the app.
                await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
                {
                    await using var db = Context();
                    await Store(db).UpdateAsync(slot, "counter", v => (v is long n ? n : 0) + 1, null, Ct);
                }));

                await using var check = Context();
                Assert.Equal(20L, (await Store(check).GetAsync(slot, "counter", Ct))!.Value);
                // One row for the shared scope, though its WorkflowId is null.
                Assert.Equal(1, await check.StorageEntries.CountAsync(e => e.UserId == userId && e.Key == "counter", Ct));
            }
            finally
            {
                await using var db = Context();
                await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(Ct);
            }
        }

        private static async Task<Guid> CreateUserAsync()
        {
            await using var db = Context();
            await db.Database.MigrateAsync(Ct);
            var user = new User("Storage test") { UserName = $"storagetest{Guid.NewGuid():N}"[..24] };
            user.NormalizedUserName = user.UserName.ToUpperInvariant();
            db.Users.Add(user);
            await db.SaveChangesAsync(Ct);
            return user.Id;
        }
    }
}
