// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using System.Text;
using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Discord;
using EchoPhase.Clients.Discord.Dto;
using EchoPhase.Clients.Twitch;
using EchoPhase.Clients.Twitch.Dto;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace EchoPhase.Clients.Tests
{
    /// <summary>
    /// A provider's failure is a failed result, never an exception: an error in
    /// its JSON, a proxy's page that is not JSON, an answer that cannot be read,
    /// the network, a timeout. Only the caller's own cancellation throws.
    /// </summary>
    public class ProviderFailureTests
    {
        private static readonly ConnectionRef Connection = new(Guid.NewGuid(), "bot");

        private sealed class Stub : HttpMessageHandler
        {
            private readonly Func<HttpResponseMessage> _answer;

            public Stub(Func<HttpResponseMessage> answer)
            {
                _answer = answer;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
                => Task.FromResult(_answer());
        }

        private static HttpResponseMessage Answer(HttpStatusCode status, string body, string type = "application/json")
            => new(status) { Content = new StringContent(body, Encoding.UTF8, type) };

        private static DiscordClient Discord(Func<HttpResponseMessage> answer)
            => new(new HttpClient(new Stub(answer)) { BaseAddress = new Uri("https://discord.test/api/") });

        private static TwitchClient Twitch(Func<HttpResponseMessage> answer)
            => new(new HttpClient(new Stub(answer)) { BaseAddress = new Uri("https://twitch.test/helix/") });

        private static Task<Result<IEnumerable<DiscordUserGuildsResponseDto>>> Guilds(DiscordClient client, CancellationToken ct = default)
            => client.GetUserGuildsAsync(new DiscordUserGuildsQueryDto(), Connection, ct);

        [Fact]
        public async Task ProviderError_InItsJson_KeepsItsStatusAndMessage()
        {
            var result = await Guilds(Discord(() => Answer(HttpStatusCode.Forbidden, """{"code":50001,"message":"Missing Access"}""")));

            Assert.False(result.IsSuccess);
            Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
            Assert.Equal("Missing Access", result.Error.Message);
        }

        [Fact]
        public async Task ProxyPage_NotJson_IsAFailureWithItsStatus()
        {
            var result = await Guilds(Discord(() => Answer(HttpStatusCode.BadGateway, "<h1>Bad gateway</h1>", "text/html")));

            Assert.False(result.IsSuccess);
            Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
            Assert.Contains("502", result.Error.Message);
        }

        [Theory]
        [InlineData("<html>maintenance</html>")]
        [InlineData("null")]
        public async Task Success_ThatCannotBeRead_IsABadGateway(string body)
        {
            var result = await Guilds(Discord(() => Answer(HttpStatusCode.OK, body)));

            Assert.False(result.IsSuccess);
            Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
        }

        public static TheoryData<Exception, HttpStatusCode> Thrown => new()
        {
            { new HttpRequestException("refused"), HttpStatusCode.BadGateway },
            { new TaskCanceledException("HttpClient.Timeout"), HttpStatusCode.GatewayTimeout },
            { new TimeoutRejectedException(), HttpStatusCode.GatewayTimeout },
            { new BrokenCircuitException(), HttpStatusCode.ServiceUnavailable },
        };

        [Theory]
        [MemberData(nameof(Thrown))]
        public async Task Thrown_IsAFailedResult(Exception thrown, HttpStatusCode status)
        {
            var discord = await Guilds(Discord(() => throw thrown));
            var twitch = await Twitch(() => throw thrown)
                .GetVipsAsync(new TwitchVipRequestQueryDto { BroadcasterId = "1" }, Connection);

            Assert.Equal(status, discord.StatusCode);
            Assert.False(discord.IsSuccess);
            Assert.Equal(status, twitch.StatusCode);
            Assert.False(twitch.IsSuccess);
        }

        [Fact]
        public async Task CallersCancellation_StillThrows()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => Guilds(Discord(() => throw new TaskCanceledException()), cts.Token));
        }

        [Fact]
        public async Task TwitchPage_IsReadWithItsCursor_AndAProxyPageFails()
        {
            var query = new TwitchVipRequestQueryDto { BroadcasterId = "1" };
            var ok = await Twitch(() => Answer(HttpStatusCode.OK,
                """{"data":[{"user_id":"7","user_name":"Ann","user_login":"ann"}],"pagination":{"cursor":"c1"}}"""))
                .GetVipsAsync(query, Connection);
            var proxy = await Twitch(() => Answer(HttpStatusCode.ServiceUnavailable, "<h1>busy</h1>", "text/html"))
                .GetVipsAsync(query, Connection);

            Assert.True(ok.IsSuccess);
            Assert.Equal("ann", Assert.Single(ok.Value).UserLogin);
            Assert.Equal("c1", ok.Page?.Cursor);
            Assert.False(proxy.IsSuccess);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, proxy.StatusCode);
        }
    }
}
