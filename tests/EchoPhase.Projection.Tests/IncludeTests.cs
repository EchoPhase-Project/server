using EchoPhase.Projection.Tests.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Projection.Tests
{
    public class IncludeTests : TestBase, IClassFixture<ConfiguredFixture>
    {
        private readonly Projector _projector;

        public IncludeTests(ConfiguredFixture fixture) =>
            _projector = fixture.Provider.GetRequiredService<Projector>();

        [Fact]
        public void SingleField_OnlyThatFieldReturned()
        {
            var result = AsDictionary(
                _projector.For(new SimpleModel { Name = "Bob" })
                    .Include(x => x.Name)
                    .Build());

            Assert.True(result.ContainsKey("Name"));
            Assert.False(result.ContainsKey("Id"));
            Assert.False(result.ContainsKey("Secret"));
        }

        [Fact]
        public void MultipleFields_AllReturned()
        {
            var result = AsDictionary(
                _projector.For(new SimpleModel())
                    .Include(x => x.Id, x => x.Name)
                    .Build());

            Assert.True(result.ContainsKey("Id"));
            Assert.True(result.ContainsKey("Name"));
            Assert.False(result.ContainsKey("Secret"));
        }

        [Fact]
        public void ChainedInclude_AllFieldsMerged()
        {
            var result = AsDictionary(
                _projector.For(new SimpleModel())
                    .Include(x => x.Id)
                    .Include(x => x.Name)
                    .Build());

            Assert.True(result.ContainsKey("Id"));
            Assert.True(result.ContainsKey("Name"));
        }

        [Fact]
        public void NestedPath_OnlySpecifiedSubField()
        {
            var result = AsDictionary(
                _projector.For(new NestedModel())
                    .Include(x => x.Address.City)
                    .Build());

            var address = AsDictionary(result["Address"]!);
            Assert.True(address.ContainsKey("City"));
            Assert.False(address.ContainsKey("Country"));
        }

        [Fact]
        public void NoFilter_AllPublicPropertiesIncluded()
        {
            var result = AsDictionary(
                _projector.For(new SimpleModel()).Build());

            Assert.True(result.ContainsKey("Id"));
            Assert.True(result.ContainsKey("Name"));
            Assert.True(result.ContainsKey("Secret"));
            Assert.True(result.ContainsKey("InternalCode"));
        }
    }
}
