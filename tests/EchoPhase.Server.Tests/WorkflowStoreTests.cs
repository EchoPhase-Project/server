// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Runner;
using EchoPhase.DAL.Postgres;
using EchoPhase.Runners.Blocks.Storage;
using EchoPhase.Server.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace EchoPhase.Server.Tests
{
    /// <summary>
    /// WorkflowStore's rules (scopes, expiry, limits) on EF's in-memory
    /// provider; the advisory lock is Postgres-only and not exercised here.
    /// </summary>
    public class WorkflowStoreTests : IDisposable
    {
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
        private readonly PostgresContext _db;
        private readonly RunnerOptions _options = new();
        private readonly StorageSlot _slot = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        public WorkflowStoreTests()
        {
            _db = new PostgresContext(new DbContextOptionsBuilder<PostgresContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
        }

        public void Dispose() => _db.Dispose();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private WorkflowStore Store() => new(_db, Options.Create(_options), _time);

        [Fact]
        public async Task KeepsValuesAsTheLanguageHasThem()
        {
            var store = Store();
            var value = new Dictionary<string, object?> { ["n"] = 3L, ["tags"] = new List<object?> { "a", "b" } };
            await store.SetAsync(_slot, "k", value, null, Ct);

            var item = Must.Present(await store.GetAsync(_slot, "k", Ct));
            var read = Assert.IsType<Dictionary<string, object?>>(item.Value);
            Assert.Equal(3L, read["n"]);
            Assert.Equal(["a", "b"], Assert.IsType<List<object?>>(read["tags"]));
            Assert.Equal("""{"n":3,"tags":["a","b"]}""".Length, item.Size);
            Assert.Null(await store.GetAsync(_slot, "missing", Ct));
        }

        [Fact]
        public async Task ScopesAndUsersAreApart()
        {
            var store = Store();
            var shared = _slot with { WorkflowId = null };
            var otherUser = _slot with { UserId = Guid.NewGuid() };
            await store.SetAsync(_slot, "k", "workflow", null, Ct);
            await store.SetAsync(shared, "k", "shared", null, Ct);
            await store.SetAsync(otherUser, "k", "other", null, Ct);

            Assert.Equal("workflow", Must.Present(await store.GetAsync(_slot, "k", Ct)).Value);
            Assert.Equal("shared", Must.Present(await store.GetAsync(shared, "k", Ct)).Value);
            Assert.Equal("other", Must.Present(await store.GetAsync(otherUser, "k", Ct)).Value);
            Assert.Single(await store.ListAsync(shared, null, null, 10, values: false, Ct));
        }

        [Fact]
        public async Task ExpiredValuesAreGone()
        {
            var store = Store();
            await store.SetAsync(_slot, "k", 1L, TimeSpan.FromMinutes(1), Ct);
            Must.Present(await store.GetAsync(_slot, "k", Ct));

            _time.Advance(TimeSpan.FromSeconds(61));
            Assert.Null(await store.GetAsync(_slot, "k", Ct));
            Assert.Empty(await store.ListAsync(_slot, null, null, 10, values: false, Ct));
            Assert.False(await store.DeleteAsync(_slot, "k", Ct));

            // An update starts over from nothing.
            await store.SetAsync(_slot, "k", 5L, TimeSpan.FromMinutes(1), Ct);
            _time.Advance(TimeSpan.FromMinutes(2));
            object? seen = "not called";
            await store.UpdateAsync(_slot, "k", current => { seen = current; return 1L; }, null, Ct);
            Assert.Null(seen);
            Assert.Null(Must.Present(await store.GetAsync(_slot, "k", Ct)).ExpiresAt);
        }

        [Fact]
        public async Task UpdateKeepsTheExpiryUnlessGivenOne()
        {
            var store = Store();
            await store.SetAsync(_slot, "n", 1L, TimeSpan.FromHours(1), Ct);
            var expires = Must.Present(await store.GetAsync(_slot, "n", Ct)).ExpiresAt;

            Assert.Equal(2L, await store.UpdateAsync(_slot, "n", v => (long)(v ?? 0L) + 1, null, Ct));
            Assert.Equal(expires, Must.Present(await store.GetAsync(_slot, "n", Ct)).ExpiresAt);

            await store.UpdateAsync(_slot, "n", v => v, TimeSpan.FromDays(1), Ct);
            Assert.Equal(_time.GetUtcNow().UtcDateTime.AddDays(1), Must.Present(await store.GetAsync(_slot, "n", Ct)).ExpiresAt);

            // Set without a ttl makes it permanent.
            await store.SetAsync(_slot, "n", 9L, null, Ct);
            Assert.Null(Must.Present(await store.GetAsync(_slot, "n", Ct)).ExpiresAt);
        }

        [Fact]
        public async Task ListsByKeyWithPrefixAndCursor()
        {
            var store = Store();
            foreach (var key in new[] { "b:2", "a:1", "b:1", "b:3" })
                await store.SetAsync(_slot, key, key, null, Ct);

            var first = await store.ListAsync(_slot, "b:", null, 2, values: true, Ct);
            Assert.Equal(["b:1", "b:2"], first.Select(i => i.Key));
            Assert.Equal("b:1", first[0].Value);
            var next = await store.ListAsync(_slot, "b:", first[^1].Key, 2, values: false, Ct);
            Assert.Equal(["b:3"], next.Select(i => i.Key));
            Assert.Null(next[0].Value);
        }

        [Fact]
        public async Task RefusesValuesAndKeysPastTheLimits()
        {
            _options.Blocks.Storage.MaxValueBytes = 10;
            _options.Blocks.Storage.MaxEntriesPerUser = 2;
            _options.Blocks.Storage.MaxBytesPerUser = 12;
            var store = Store();

            await Assert.ThrowsAsync<StorageLimitException>(() => store.SetAsync(_slot, "big", new string('x', 20), null, Ct));
            await Assert.ThrowsAsync<StorageLimitException>(() => store.SetAsync(_slot, new string('k', 201), 1L, null, Ct));

            await store.SetAsync(_slot, "a", "12345", null, Ct); // 7 bytes as JSON
            await store.SetAsync(_slot, "b", 1L, null, Ct);
            // A third key is one too many; rewriting one is fine.
            await Assert.ThrowsAsync<StorageLimitException>(() => store.SetAsync(_slot, "c", 1L, null, Ct));
            await store.SetAsync(_slot, "b", 2L, null, Ct);
            // 7 + 8 bytes would be over 12.
            await Assert.ThrowsAsync<StorageLimitException>(() => store.SetAsync(_slot, "b", "123456", null, Ct));

            // Expired values do not count.
            await store.SetAsync(_slot, "b", 3L, TimeSpan.FromSeconds(1), Ct);
            _time.Advance(TimeSpan.FromSeconds(2));
            await store.SetAsync(_slot, "c", 1L, null, Ct);
        }

        [Fact]
        public void EachKeyAndScopeHasItsOwnLock()
        {
            var id = WorkflowStore.LockId(_slot, "k");
            Assert.Equal(id, WorkflowStore.LockId(_slot, "k"));
            Assert.NotEqual(id, WorkflowStore.LockId(_slot, "k2"));
            Assert.NotEqual(id, WorkflowStore.LockId(_slot with { WorkflowId = null }, "k"));
        }
    }
}
