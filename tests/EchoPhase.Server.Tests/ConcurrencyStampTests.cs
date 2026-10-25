// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.Server.Tests
{
    /// <summary>Every IConcurrentEntity's stamp is checked on save and renewed by it.</summary>
    public class ConcurrencyStampTests
    {
        private static PostgresContext Context(string name) => new(new DbContextOptionsBuilder<PostgresContext>()
            .UseInMemoryDatabase(name).Options);

        [Fact]
        public void EveryStamp_IsAConcurrencyToken()
        {
            using var db = Context(Guid.NewGuid().ToString());

            var unchecked_ =
                from type in db.Model.GetEntityTypes()
                where typeof(IConcurrentEntity).IsAssignableFrom(type.ClrType)
                let stamp = type.FindProperty(nameof(IConcurrentEntity.ConcurrencyStamp))
                where stamp is null || !stamp.IsConcurrencyToken
                select type.ClrType.Name;

            Assert.Empty(unchecked_);
        }

        [Fact]
        public async Task Save_RenewsTheStamp()
        {
            using var db = Context(Guid.NewGuid().ToString());
            var user = new User("A") { UserName = "a" };
            db.Users.Add(user);
            var note = new Note { OwnerId = user.Id, Title = "t" };
            db.Notes.Add(note);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var first = note.ConcurrencyStamp;

            note.Title = "u";
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.NotEqual(first, note.ConcurrencyStamp);
        }
    }
}
