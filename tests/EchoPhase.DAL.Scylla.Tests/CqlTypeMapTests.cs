// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using EchoPhase.DAL.Scylla.Builders;
using EchoPhase.DAL.Scylla.Schema;

namespace EchoPhase.DAL.Scylla.Tests
{
    public class CqlTypeMapTests
    {
        [Theory]
        [InlineData(typeof(string), "text")]
        [InlineData(typeof(Guid), "uuid")]
        [InlineData(typeof(int?), "int")]
        [InlineData(typeof(IPAddress), "inet")]
        [InlineData(typeof(List<string>), "list<text>")]
        [InlineData(typeof(Dictionary<string, int>), "map<text, int>")]
        public void For_ReturnsCanonicalType(Type type, string expected)
            => Assert.Equal(expected, CqlTypeMap.For(type));

        [Theory]
        [InlineData(typeof(Guid), "timeuuid", true)]
        [InlineData(typeof(string), "varchar", true)]
        [InlineData(typeof(Dictionary<string, int>), "frozen<map<text, int>>", true)]
        [InlineData(typeof(List<int>), "list<text>", false)]
        [InlineData(typeof(string), "int", false)]
        public void IsCompatible_ComparesWithSystemSchemaTypes(Type type, string cqlType, bool expected)
            => Assert.Equal(expected, CqlTypeMap.IsCompatible(type, cqlType));

        [Fact]
        public void TableBuilder_ColumnOfT_UsesMappedType()
        {
            var cql = new TableBuilder("audit", "ks")
                .Column<HashSet<string>>("tags")
                .Uuid("id")
                .PrimaryKey("id")
                .Build();

            Assert.Contains("tags set<text>", cql);
        }

        [Fact]
        public void TableBuilder_Named_SetsNameWhenCreatedWithoutOne()
        {
            var cql = new TableBuilder("", "ks").Named("audit").Uuid("id").PrimaryKey("id").Build();

            Assert.StartsWith("CREATE TABLE ks.audit", cql);
        }
    }
}
