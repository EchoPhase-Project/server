using EchoPhase.Projection.Tests.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Projection.Tests
{
    public class IncludeCollectionTests : TestBase, IClassFixture<ConfiguredFixture>
    {
        private readonly Projector _projector;

        public IncludeCollectionTests(ConfiguredFixture fixture) =>
            _projector = fixture.Provider.GetRequiredService<Projector>();

        [Fact]
        public void AppliesConfigToEachElement()
        {
            var user = new UserModel
            {
                Tokens = new List<TokenModel>
                {
                    new() { DeviceId = "Chrome",  RefreshValue = "secret1" },
                    new() { DeviceId = "Firefox", RefreshValue = "secret2" },
                }
            };

            var result = AsDictionary(
                _projector.For(user)
                    .Include(u => u.Id, u => u.Name)
                    .IncludeCollection(u => u.Tokens, b => b
                        .Include(t => t.Id, t => t.DeviceId))
                    .Build());

            Assert.True(result.ContainsKey("Tokens"));
            Assert.False(result.ContainsKey("PasswordHash"));

            var tokens = AsList(result["Tokens"]);
            Assert.Equal(2, tokens.Count);

            var first = AsDictionary(tokens[0]);
            Assert.True(first.ContainsKey("Id"));
            Assert.True(first.ContainsKey("DeviceId"));
            Assert.False(first.ContainsKey("RefreshValue"));
            Assert.False(first.ContainsKey("CreatedAt"));
        }

        [Fact]
        public void WithExposeOnElements()
        {
            var user = new UserModel
            {
                Tokens = new List<TokenModel>
                {
                    new() { DeviceId = "Safari", RefreshValue = "secret" }
                }
            };

            var result = AsDictionary(
                _projector.For(user)
                    .Include(u => u.Name)
                    .IncludeCollection(u => u.Tokens, b => b
                        .WithOptions(o => o.IncludeOnlyExpose = true))
                    .Build());

            var token = AsDictionary(AsList(result["Tokens"])[0]);
            Assert.True(token.ContainsKey("Id"));
            Assert.True(token.ContainsKey("DeviceId"));
            Assert.False(token.ContainsKey("RefreshValue"));
        }

        [Fact]
        public void EmptyCollection_ReturnsEmptyList()
        {
            var result = AsDictionary(
                _projector.For(new UserModel { Tokens = new List<TokenModel>() })
                    .IncludeCollection(u => u.Tokens, b => b.Include(t => t.Id))
                    .Build());

            Assert.Empty(AsList(result["Tokens"]));
        }

        [Fact]
        public void MultipleCollections_ConfiguredIndependently()
        {
            var user = new UserModel
            {
                Tokens = new List<TokenModel> { new() { DeviceId = "Chrome" } },
                Items = new List<SimpleModel> { new() { Name = "Item1" } }
            };

            var result = AsDictionary(
                _projector.For(user)
                    .Include(u => u.Id)
                    .IncludeCollection(u => u.Tokens, b => b.Include(t => t.DeviceId))
                    .IncludeCollection(u => u.Items, b => b.Include(i => i.Name))
                    .Build());

            var tokenDict = AsDictionary(AsList(result["Tokens"])[0]);
            Assert.True(tokenDict.ContainsKey("DeviceId"));
            Assert.False(tokenDict.ContainsKey("Id"));

            var itemDict = AsDictionary(AsList(result["Items"])[0]);
            Assert.True(itemDict.ContainsKey("Name"));
            Assert.False(itemDict.ContainsKey("Secret"));
        }

        [Fact]
        public void OptionsIsolatedPerElement()
        {
            var user = new UserModel
            {
                Tokens = new List<TokenModel>
                {
                    new() { DeviceId = "A" },
                    new() { DeviceId = "B" },
                }
            };

            var tokens = AsList(
                AsDictionary(
                    _projector.For(user)
                        .IncludeCollection(u => u.Tokens, b => b.Include(t => t.DeviceId))
                        .Build())["Tokens"]);

            Assert.Equal(2, tokens.Count);
            foreach (var t in tokens)
            {
                var d = AsDictionary(t);
                Assert.True(d.ContainsKey("DeviceId"));
                Assert.False(d.ContainsKey("RefreshValue"));
            }
        }
    }
}
