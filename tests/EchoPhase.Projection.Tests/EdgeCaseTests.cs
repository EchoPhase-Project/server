// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Dynamic;
using EchoPhase.Projection.Tests.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Projection.Tests
{
    /// <summary>Default options: only [Expose] properties.</summary>
    public class ExposeEdgeCaseTests : TestBase, IClassFixture<Fixture>
    {
        private readonly Projector _projector;

        public ExposeEdgeCaseTests(Fixture fixture) =>
            _projector = fixture.Provider.GetRequiredService<Projector>();

        [Fact]
        public void ValueTypes_AreValues_NotObjects()
        {
            var model = new ValueTypesModel();
            var result = AsDictionary(_projector.For(model).Build());

            Assert.Equal(model.Day, result["Day"]);
            Assert.Equal(model.Time, result["Time"]);
            Assert.Equal(model.MaybeAt, result["MaybeAt"]);
            Assert.Equal(model.Link, result["Link"]);
        }

        [Fact]
        public void ExposedDictionary_KeepsItsEntries()
        {
            var result = AsDictionary(_projector.For(new ValueTypesModel()).Build());

            var counts = AsDictionary(result["Counts"]);
            Assert.Equal(1, counts["a"]);
            Assert.Equal(2, counts["b"]);
        }

        [Fact]
        public void RecursiveType_ProjectsEveryLevel()
        {
            var tree = new Category
            {
                Name = "root",
                Children = [new Category { Name = "child", Children = [new Category { Name = "leaf" }] }],
            };

            var root = AsDictionary(_projector.For(tree).Build());
            var child = AsDictionary(AsList(root["Children"])[0]);
            var leaf = AsDictionary(AsList(child["Children"])[0]);

            Assert.Equal("child", child["Name"]);
            Assert.Equal("leaf", leaf["Name"]);
            Assert.False(leaf.ContainsKey("Secret"));
        }

        [Fact]
        public void Only_ReturnsJustTheListedFields()
        {
            var result = AsDictionary(
                _projector.For(new NestedModel()).Only(x => x.Id).Build());

            Assert.Equal(["Id"], result.Keys);
        }

        [Fact]
        public void Only_ComplexField_StillHidesNonExposedContent()
        {
            var result = AsDictionary(
                _projector.For(new NestedModel()).Only(x => x.Address).Build());

            var address = AsDictionary(result["Address"]);
            Assert.True(address.ContainsKey("City"));
            Assert.False(address.ContainsKey("InternalCode"));
        }
    }

    /// <summary>IncludeOnlyExpose = false: everything unless Include narrows it.</summary>
    public class IncludeEdgeCaseTests : TestBase, IClassFixture<ConfiguredFixture>
    {
        private readonly Projector _projector;

        public IncludeEdgeCaseTests(ConfiguredFixture fixture) =>
            _projector = fixture.Provider.GetRequiredService<Projector>();

        [Fact]
        public void IncludedComplexProperty_KeepsItsContent()
        {
            var result = AsDictionary(
                _projector.For(new NestedModel()).Include(x => x.Address).Build());

            var address = AsDictionary(result["Address"]);
            Assert.Equal("Moscow", address["City"]);
            Assert.False(result.ContainsKey("Hidden"));
        }

        [Fact]
        public void Expando_IsUsedAtEveryLevel()
        {
            dynamic result = _projector.For(new NestedModel()).BuildExpando();

            Assert.IsType<ExpandoObject>(result);
            Assert.IsType<ExpandoObject>(result.Address);
        }
    }
}
