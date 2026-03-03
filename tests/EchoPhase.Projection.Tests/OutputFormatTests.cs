using System.Dynamic;
using EchoPhase.Projection.Tests.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Projection.Tests
{
    public class OutputFormatTests : TestBase, IClassFixture<ConfiguredFixture>
    {
        private readonly Projector _projector;

        public OutputFormatTests(ConfiguredFixture fixture) =>
            _projector = fixture.Provider.GetRequiredService<Projector>();

        [Fact]
        public void BuildDictionary_ReturnsDictionary()
        {
            var result = _projector.For(new SimpleModel { Name = "Dict" })
                .Include(x => x.Name)
                .BuildDictionary();

            Assert.IsType<Dictionary<string, object?>>(result);
            Assert.Equal("Dict", result["Name"]);
        }

        [Fact]
        public void BuildExpando_ReturnsExpando()
        {
            var result = _projector.For(new SimpleModel { Name = "Expando" })
                .Include(x => x.Name)
                .BuildExpando();

            Assert.IsType<ExpandoObject>(result);
            Assert.Equal("Expando", ((IDictionary<string, object?>)result)["Name"]);
        }

        [Fact]
        public void NullSource_ReturnsNull()
        {
            Assert.Null(_projector.For<SimpleModel>(null!).Build());
        }

        [Fact]
        public void Options_IsolatedBetweenCalls()
        {
            // WithOptions на одном вызове не влияет на следующий
            var r1 = AsDictionary(
                _projector.For(new SimpleModel())
                    .WithOptions(o => o.IncludeOnlyExpose = true)
                    .Build());

            var r2 = AsDictionary(
                _projector.For(new SimpleModel()).Build());

            Assert.False(r1.ContainsKey("Secret"));
            Assert.True(r2.ContainsKey("Secret"));
        }

        [Fact]
        public void RepeatedCalls_ConsistentKeys()
        {
            var r1 = AsDictionary(
                _projector.For(new SimpleModel { Name = "First" })
                    .WithOptions(o => o.IncludeOnlyExpose = true)
                    .Build());

            var r2 = AsDictionary(
                _projector.For(new SimpleModel { Name = "Second" })
                    .WithOptions(o => o.IncludeOnlyExpose = true)
                    .Build());

            Assert.Equal(r1.Keys, r2.Keys);
            Assert.Equal("First", r1["Name"]);
            Assert.Equal("Second", r2["Name"]);
        }
    }
}
