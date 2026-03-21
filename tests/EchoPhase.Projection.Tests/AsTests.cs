// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Projection.Attributes;
using EchoPhase.Projection.Tests.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Projection.Tests
{
    public class AsTests : TestBase, IClassFixture<ConfiguredFixture>
    {
        private readonly Projector _projector;

        public AsTests(ConfiguredFixture fixture) =>
            _projector = fixture.Provider.GetRequiredService<Projector>();

        public interface IEntity
        {
            Guid Id
            {
                get; set;
            }
        }

        public interface INamedEntity : IEntity
        {
            string Name
            {
                get; set;
            }
        }

        public class ConcreteEntity : INamedEntity
        {
            [Expose] public Guid Id { get; set; } = Guid.NewGuid();
            [Expose] public string Name { get; set; } = "Entity";
            public string Secret { get; set; } = "hidden";
        }

        public class DerivedEntity : ConcreteEntity
        {
            [Expose] public string Extra { get; set; } = "extra";
            public string DerivedSecret { get; set; } = "derived-hidden";
        }

        [Fact]
        public void As_FromObject_AllowsIncludeOnConcreteType()
        {
            object source = new ConcreteEntity { Name = "Test" };

            var result = AsDictionary(
                _projector.For(source)
                    .As<ConcreteEntity>()
                    .Include(e => e.Id, e => e.Name)
                    .Build());

            Assert.True(result.ContainsKey("Id"));
            Assert.True(result.ContainsKey("Name"));
            Assert.False(result.ContainsKey("Secret"));
        }

        [Fact]
        public void As_FromInterface_AllowsIncludeOnConcreteType()
        {
            INamedEntity source = new ConcreteEntity { Name = "FromInterface" };

            var result = AsDictionary(
                _projector.For(source)
                    .As<ConcreteEntity>()
                    .Include(e => e.Name)
                    .Build());

            Assert.True(result.ContainsKey("Name"));
            Assert.Equal("FromInterface", result["Name"]);
            Assert.False(result.ContainsKey("Secret"));
        }

        [Fact]
        public void As_FromInterface_WithExpose_ResolvesExposeFromConcreteType()
        {
            INamedEntity source = new ConcreteEntity { Name = "Exposed" };

            var result = AsDictionary(
                _projector.For(source)
                    .As<ConcreteEntity>()
                    .WithOptions(o => o.IncludeOnlyExpose = true)
                    .Build());

            Assert.True(result.ContainsKey("Id"));
            Assert.True(result.ContainsKey("Name"));
            Assert.False(result.ContainsKey("Secret"));
        }

        [Fact]
        public void As_FromBaseClass_ToDerivedClass_IncludesDerivedFields()
        {
            ConcreteEntity source = new DerivedEntity { Name = "Derived", Extra = "bonus" };

            var result = AsDictionary(
                _projector.For(source)
                    .As<DerivedEntity>()
                    .Include(e => e.Name, e => e.Extra)
                    .Build());

            Assert.True(result.ContainsKey("Name"));
            Assert.True(result.ContainsKey("Extra"));
            Assert.False(result.ContainsKey("Secret"));
            Assert.False(result.ContainsKey("DerivedSecret"));
        }

        [Fact]
        public void As_FromBaseClass_WithExpose_IncludesDerivedExposedFields()
        {
            ConcreteEntity source = new DerivedEntity { Extra = "bonus" };

            var result = AsDictionary(
                _projector.For(source)
                    .As<DerivedEntity>()
                    .WithOptions(o => o.IncludeOnlyExpose = true)
                    .Build());

            Assert.True(result.ContainsKey("Id"));
            Assert.True(result.ContainsKey("Name"));
            Assert.True(result.ContainsKey("Extra"));
            Assert.False(result.ContainsKey("DerivedSecret"));
        }

        [Fact]
        public void As_InvalidCast_Throws()
        {
            object source = new ConcreteEntity();

            Assert.Throws<InvalidCastException>(() =>
                _projector.For(source).As<TokenModel>());
        }

        [Fact]
        public void As_CorrectValues_PreservedAfterCast()
        {
            IEntity source = new ConcreteEntity
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000042"),
                Name = "Preserved"
            };

            var result = AsDictionary(
                _projector.For(source)
                    .As<ConcreteEntity>()
                    .Include(e => e.Id, e => e.Name)
                    .Build());

            Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000042"), result["Id"]);
            Assert.Equal("Preserved", result["Name"]);
        }

        [Fact]
        public void As_PreservesOptions()
        {
            object source = new ConcreteEntity { Name = "Options" };

            var result = AsDictionary(
                _projector.For(source)
                    .As<ConcreteEntity>()
                    .WithOptions(o => o.IncludeOnlyExpose = true)
                    .Build());

            Assert.False(result.ContainsKey("Secret"));
            Assert.True(result.ContainsKey("Id"));
        }
    }
}
