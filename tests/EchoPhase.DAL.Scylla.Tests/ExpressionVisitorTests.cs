// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Tests
{
    public class ExpressionVisitorTests
    {
        private readonly Guid _id = Guid.NewGuid();

        [Fact]
        public void PartitionQuery_WithClusteringOrderAndLimit_TranslatesWithoutFiltering()
        {
            var query = QueryHarness.Query<AuditEntry>()
                .Where(x => x.ImpersonationId == _id)
                .OrderByDescending(x => x.At)
                .Take(50);

            var (cql, parameters) = QueryHarness.Translate(query, QueryHarness.AuditModel());

            Assert.Equal("SELECT * FROM ks.audit WHERE impersonation_id = ? ORDER BY at DESC LIMIT 50", cql);
            Assert.Equal(new object[] { _id }, parameters);
        }

        [Fact]
        public void FilterOnRegularColumn_WithoutAllowFiltering_Throws()
        {
            var query = QueryHarness.Query<AuditEntry>().Where(x => x.Status == 500);

            var ex = Assert.Throws<NotSupportedException>(() => QueryHarness.Translate(query, QueryHarness.AuditModel()));
            Assert.Contains("AllowFiltering", ex.Message);
        }

        [Fact]
        public void FilterOnRegularColumn_WithAllowFiltering_AppendsAllowFiltering()
        {
            var query = QueryHarness.AllowFiltering(
                QueryHarness.Query<AuditEntry>().Where(x => x.Status == 500));

            var (cql, _) = QueryHarness.Translate(query, QueryHarness.AuditModel());

            Assert.Equal("SELECT * FROM ks.audit WHERE status = ? ALLOW FILTERING", cql);
        }

        [Fact]
        public void CollectionContains_RequiresAllowFiltering()
        {
            var query = QueryHarness.Query<AuditEntry>()
                .Where(x => x.ImpersonationId == _id && x.Tags!.Contains("admin"));

            Assert.Throws<NotSupportedException>(() => QueryHarness.Translate(query, QueryHarness.AuditModel()));
        }

        [Fact]
        public void ContainsOnValues_TranslatesToTypedIn()
        {
            var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var query = QueryHarness.Query<AuditEntry>().Where(x => ids.Contains(x.ImpersonationId));

            var (cql, parameters) = QueryHarness.Translate(query, QueryHarness.AuditModel());

            Assert.Equal("SELECT * FROM ks.audit WHERE impersonation_id IN ?", cql);
            Assert.Equal(ids, Assert.IsType<List<Guid>>(Assert.Single(parameters)));
        }

        [Fact]
        public void OrderBy_WithoutPartitionRestriction_Throws()
        {
            var query = QueryHarness.Query<AuditEntry>().OrderBy(x => x.At);

            Assert.Throws<NotSupportedException>(() => QueryHarness.Translate(query, QueryHarness.AuditModel()));
        }

        [Fact]
        public void Or_IsRejected()
        {
            var query = QueryHarness.Query<AuditEntry>()
                .Where(x => x.ImpersonationId == _id || x.ImpersonationId == Guid.Empty);

            Assert.Throws<NotSupportedException>(() => QueryHarness.Translate(query, QueryHarness.AuditModel()));
        }

        [Fact]
        public void Select_ProjectsOnlyUsedColumns()
        {
            var query = QueryHarness.Query<AuditEntry>()
                .Where(x => x.ImpersonationId == _id)
                .Select(x => new { x.Method, x.Path });

            var (cql, _) = QueryHarness.Translate(query, QueryHarness.AuditModel());

            Assert.Equal("SELECT method, path FROM ks.audit WHERE impersonation_id = ?", cql);
        }

        [Fact]
        public void BooleanColumn_TranslatesToEquality()
        {
            var query = QueryHarness.AllowFiltering(QueryHarness.Query<AuditEntry>().Where(x => !x.Flagged));

            var (cql, parameters) = QueryHarness.Translate(query, QueryHarness.AuditModel());

            Assert.Equal("SELECT * FROM ks.audit WHERE flagged = ? ALLOW FILTERING", cql);
            Assert.Equal(new object[] { false }, parameters);
        }
    }
}
