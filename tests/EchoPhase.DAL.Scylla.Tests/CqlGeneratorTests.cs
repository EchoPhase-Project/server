// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Cassandra;
using EchoPhase.DAL.Scylla.Cql;
using EchoPhase.DAL.Scylla.Enums;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Tests
{
    public class CqlGeneratorTests
    {
        private readonly CqlGenerator _generator = new();
        private readonly AuditEntry _entry = new() { ImpersonationId = Guid.NewGuid(), At = Guid.NewGuid(), Method = "GET" };

        [Fact]
        public void Insert_SendsNullsAsUnset()
        {
            var (cql, parameters) = _generator.GenerateInsert(_entry, QueryHarness.AuditModel());

            Assert.StartsWith("INSERT INTO ks.audit (", cql);
            Assert.Contains(parameters, p => ReferenceEquals(p, Unset.Value));
            Assert.DoesNotContain(parameters, p => p is null);
        }

        [Fact]
        public void Update_SendsNullsAsUnset_AndPutsUsingBeforeSet()
        {
            var options = new WriteOptions(Ttl: TimeSpan.FromHours(1));

            var (cql, parameters) = _generator.GenerateUpdate(_entry, QueryHarness.AuditModel(), options);

            Assert.StartsWith("UPDATE ks.audit USING TTL ? SET ", cql);
            Assert.Equal(3600, parameters[0]);
            Assert.DoesNotContain(parameters, p => p is null);
        }

        [Fact]
        public void Delete_WithTtl_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _generator.GenerateDelete(_entry, QueryHarness.AuditModel(), new WriteOptions(Ttl: TimeSpan.FromMinutes(1))));
        }

        [Fact]
        public void Delete_WithTimestamp_UsesMicroseconds()
        {
            var timestamp = DateTimeOffset.UnixEpoch.AddSeconds(1);

            var (cql, parameters) = _generator.GenerateDelete(_entry, QueryHarness.AuditModel(), new WriteOptions(Timestamp: timestamp));

            Assert.Equal("DELETE FROM ks.audit USING TIMESTAMP ? WHERE impersonation_id = ? AND at = ?", cql);
            Assert.Equal(1_000_000L, parameters[0]);
        }

        [Fact]
        public void ClearColumns_GeneratesColumnDelete()
        {
            var (cql, _) = _generator.GenerateClearColumns(_entry, QueryHarness.AuditModel(), ["Path", "Status"]);

            Assert.Equal("DELETE path, status FROM ks.audit WHERE impersonation_id = ? AND at = ?", cql);
        }

        [Fact]
        public void ClearColumns_OnKey_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _generator.GenerateClearColumns(_entry, QueryHarness.AuditModel(), ["At"]));
        }

        [Fact]
        public void Generate_ModifiedWithClears_UpdatesOtherColumnsAndDeletesClearedOnes()
        {
            var entry = new AuditEntry { ImpersonationId = _entry.ImpersonationId, At = _entry.At, Method = "GET" };
            var tracked = new TrackedEntity(typeof(AuditEntry), entry, EntityState.Modified);
            tracked.ClearedColumns.Add("Method");

            var statements = _generator.Generate(tracked, QueryHarness.AuditModel());

            // Flagged is a non-nullable bool, so it always has a value to write;
            // the cleared column must not be SET even though it holds one.
            Assert.Equal(2, statements.Count);
            Assert.StartsWith("UPDATE ks.audit SET ", statements[0].cql);
            Assert.DoesNotContain("method =", statements[0].cql);
            Assert.StartsWith("DELETE method FROM ks.audit", statements[1].cql);
        }

        [Fact]
        public void Generate_ModifiedWithOnlyClears_AndNothingToSet_EmitsOnlyTheColumnDelete()
        {
            var entry = new KeyOnlyWithNote { Id = Guid.NewGuid(), Note = "x" };
            var builder = new EchoPhase.DAL.Scylla.Builders.EntityBuilder<KeyOnlyWithNote>();
            builder.ToTable("notes", "ks");
            builder.HasPartitionKey<object>(x => x.Id);
            var tracked = new TrackedEntity(typeof(KeyOnlyWithNote), entry, EntityState.Modified);
            tracked.ClearedColumns.Add("Note");

            var statements = _generator.Generate(tracked, builder);

            var (cql, _) = Assert.Single(statements);
            Assert.Equal("DELETE note FROM ks.notes WHERE id = ?", cql);
        }

        public class KeyOnlyWithNote
        {
            public Guid Id { get; set; }
            public string? Note { get; set; }
        }
    }
}
