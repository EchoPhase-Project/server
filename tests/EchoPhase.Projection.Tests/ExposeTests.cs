using Microsoft.Extensions.DependencyInjection;
using EchoPhase.Projection.Tests.Models;

namespace EchoPhase.Projection.Tests
{
    public class ExposeTests : TestBase, IClassFixture<Fixture>
    {
        private readonly Projector _projector;

        public ExposeTests(Fixture fixture) =>
            _projector = fixture.Provider.GetRequiredService<Projector>();

        [Fact]
        public void ExcludesNonExposedFields()
        {
            var result = AsDictionary(
                _projector.For(new SimpleModel()).Build());

            Assert.True(result.ContainsKey("Id"));
            Assert.True(result.ContainsKey("Name"));
            Assert.False(result.ContainsKey("Secret"));
            Assert.False(result.ContainsKey("InternalCode"));
        }

        [Fact]
        public void CorrectValues()
        {
            var result = AsDictionary(
                _projector.For(new SimpleModel { Name = "Alice" }).Build());

            Assert.Equal("Alice", result["Name"]);
        }

        [Fact]
        public void NestedExposedPropertiesIncluded()
        {
            var result = AsDictionary(
                _projector.For(new NestedModel()).Build());

            Assert.True(result.ContainsKey("Address"));
            Assert.False(result.ContainsKey("Hidden"));

            var address = AsDictionary(result["Address"]!);
            Assert.True(address.ContainsKey("City"));
            Assert.False(address.ContainsKey("InternalCode"));
        }

        [Fact]
        public void PageModel_ExposedFieldsOnly()
        {
            var page = new PageModel<SimpleModel>
            {
                Data = new List<SimpleModel> { new() { Name = "Alice" } }
            };

            var result = AsDictionary(_projector.For(page).Build());

            Assert.True(result.ContainsKey("Data"));
            Assert.True(result.ContainsKey("HasMore"));

            var items = AsList(result["Data"]);
            var first = AsDictionary(items[0]);
            Assert.False(first.ContainsKey("Secret"));
        }

        [Fact]
        public void PageModel_HasMore_FalseWhenNoCursor()
        {
            var result = AsDictionary(
                _projector.For(new PageModel<SimpleModel>()).Build());

            Assert.Equal(false, result["HasMore"]);
        }

        [Fact]
        public void PageModel_HasMore_TrueWhenCursorPresent()
        {
            var result = AsDictionary(
                _projector.For(new PageModel<SimpleModel> { NextCursor = "cursor123" }).Build());

            Assert.Equal(true, result["HasMore"]);
            Assert.Equal("cursor123", result["NextCursor"]);
        }

        [Fact]
        public void IncludeOnlyExpose_WithExplicitFields_MergesBoth()
        {
            var model = new SimpleModel
            {
                Name = "Alice",
                Secret = "visible-secret",
                InternalCode = 99
            };

            var result = AsDictionary(
                _projector.For(model)
                    .Include(x => x.Secret)
                    .Build());

            Assert.True(result.ContainsKey("Id"));
            Assert.True(result.ContainsKey("Name"));
            Assert.Equal("Alice", result["Name"]);

            Assert.True(result.ContainsKey("Secret"));
            Assert.Equal("visible-secret", result["Secret"]);

            Assert.False(result.ContainsKey("InternalCode"));
        }
    }
}
