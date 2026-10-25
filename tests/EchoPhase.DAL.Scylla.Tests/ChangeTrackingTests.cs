// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Builders;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Enums;
using EchoPhase.DAL.Scylla.InMemory;
using EchoPhase.DAL.Scylla.Linq;

namespace EchoPhase.DAL.Scylla.Tests
{
    public class Profile
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Bio { get; set; }
        public List<string>? Tags { get; set; }
    }

    internal sealed class ProfileContext : DbContext
    {
        public DbSet<Profile> Profiles { get; set; } = null!;

        public ProfileContext(ScyllaConnection connection) : base(connection)
        {
        }

        protected override void OnModelBuilding(ModelBuilder builder)
        {
            var profile = builder.Entity<Profile>();
            profile.ToTable("profiles").HasPartitionKey<Guid>(p => p.Id);
            profile.SetColumnName(nameof(Profile.Id), "id");
            profile.SetColumnName(nameof(Profile.Name), "name");
            profile.SetColumnName(nameof(Profile.Bio), "bio");
            profile.SetColumnName(nameof(Profile.Tags), "tags");
        }
    }

    /// <summary>Loaded entities are tracked like EF's: changes found, only they written.</summary>
    public class ChangeTrackingTests : IDisposable
    {
        private readonly InMemoryCqlEngine _engine = new("echophase");
        private readonly ScyllaConnection _connection;
        private readonly Guid _id = Guid.NewGuid();

        public ChangeTrackingTests()
        {
            _connection = new ScyllaConnection(_engine, "echophase");
            _engine.Execute(new CqlStatement("CREATE TABLE profiles (id uuid PRIMARY KEY, name text, bio text, tags list<text>)", []));
            _engine.Execute(new CqlStatement("INSERT INTO profiles (id, name, bio, tags) VALUES (?, 'Ann', 'hi', ['a'])", [_id]));
        }

        public void Dispose() => _connection.Dispose();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private ProfileContext Context() => new(_connection);

        private string[] Writes() => _engine.Log.Where(c => c.StartsWith("UPDATE") || c.StartsWith("INSERT INTO profiles")).ToArray();

        [Fact]
        public async Task ALoadedEntity_SavesOnlyWhatChanged()
        {
            using var db = Context();
            var profile = await db.Profiles.Where(p => p.Id == _id).FirstAsync(Ct);
            _engine.Log.Clear();

            profile.Name = "Anna";
            Assert.Equal(1, await db.SaveChangesAsync(Ct));

            var update = Assert.Single(Writes());
            Assert.Contains("SET name = ?", update);
            Assert.DoesNotContain("bio", update);
            using var other = Context();
            var stored = await other.Profiles.Where(p => p.Id == _id).FirstAsync(Ct);
            Assert.Equal(("Anna", "hi"), (stored.Name, stored.Bio));
        }

        [Fact]
        public async Task SettingNull_ClearsTheStoredValue()
        {
            using var db = Context();
            var profile = await db.Profiles.Where(p => p.Id == _id).FirstAsync(Ct);

            profile.Bio = null;
            await db.SaveChangesAsync(Ct);

            using var other = Context();
            Assert.Null((await other.Profiles.Where(p => p.Id == _id).FirstAsync(Ct)).Bio);
        }

        [Fact]
        public async Task NothingChanged_WritesNothing_AndASavedEntityStaysTracked()
        {
            using var db = Context();
            var profile = await db.Profiles.Where(p => p.Id == _id).FirstAsync(Ct);
            _engine.Log.Clear();

            Assert.Equal(0, await db.SaveChangesAsync(Ct));
            profile.Bio = "one";
            await db.SaveChangesAsync(Ct);
            profile.Bio = "two";
            await db.SaveChangesAsync(Ct);

            Assert.Equal(2, Writes().Length);
            Assert.Equal(EntityState.Unchanged, db.Database.Entries().Single().State);
        }

        [Fact]
        public async Task TheSameRow_IsTheSameInstance_UnlessNotTracked()
        {
            using var db = Context();
            var first = await db.Profiles.Where(p => p.Id == _id).FirstAsync(Ct);
            first.Name = "changed locally";

            var second = await db.Profiles.Where(p => p.Id == _id).FirstAsync(Ct);
            var fresh = await db.Profiles.Where(p => p.Id == _id).AsNoTracking().FirstAsync(Ct);

            Assert.Same(first, second);
            Assert.Equal("changed locally", second.Name);
            Assert.NotSame(first, fresh);
            Assert.Equal("Ann", fresh.Name);
        }

        [Fact]
        public async Task ACollectionChangedInPlace_IsSaved()
        {
            using var db = Context();
            var profile = await db.Profiles.Where(p => p.Id == _id).FirstAsync(Ct);

            profile.Tags!.Add("b");
            await db.SaveChangesAsync(Ct);

            using var other = Context();
            Assert.Equal(["a", "b"], (await other.Profiles.Where(p => p.Id == _id).FirstAsync(Ct)).Tags);
        }

        [Fact]
        public async Task Untracked_ChangesAreNotSaved()
        {
            using var db = Context();
            var profile = await db.Profiles.Where(p => p.Id == _id).AsNoTracking().FirstAsync(Ct);

            profile.Name = "ignored";
            Assert.Equal(0, await db.SaveChangesAsync(Ct));
        }
    }
}
