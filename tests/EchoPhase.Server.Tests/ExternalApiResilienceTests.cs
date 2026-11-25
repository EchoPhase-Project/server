// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using EchoPhase.Server.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Server.Tests
{
    /// <summary>
    /// Calls to Discord and Twitch repeat only what may succeed on a second try
    /// (a transient failure or 429, a safe method) and stop at any other answer.
    /// It used to repeat a GET six times on 404 too, for 126 s, past HttpClient's
    /// own timeout.
    /// </summary>
    public class ExternalApiResilienceTests
    {
        private sealed class Script : HttpMessageHandler
        {
            private readonly Queue<Func<HttpResponseMessage>> _answers;

            public Script(params Func<HttpResponseMessage>[] answers)
            {
                _answers = new(answers);
            }

            public int Calls { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Calls++;
                return Task.FromResult(_answers.Count > 1 ? _answers.Dequeue()() : _answers.Peek()());
            }
        }

        private static Func<HttpResponseMessage> Status(HttpStatusCode code, string? retryAfter = null) => () =>
        {
            var response = new HttpResponseMessage(code);
            if (retryAfter is not null)
                response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
            return response;
        };

        private static async Task<(HttpStatusCode Status, int Calls)> SendAsync(HttpMethod method, params Func<HttpResponseMessage>[] answers)
        {
            var script = new Script(answers);
            var services = new ServiceCollection();
            services.AddHttpClient("provider", c => c.BaseAddress = new Uri("https://provider.test/"))
                .ConfigurePrimaryHttpMessageHandler(() => script)
                .AddExternalApiResilience(o => o.Retry.Delay = TimeSpan.Zero);
            using var provider = services.BuildServiceProvider();
            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("provider");

            using var response = await client.SendAsync(new HttpRequestMessage(method, "x"), TestContext.Current.CancellationToken);
            return (response.StatusCode, script.Calls);
        }

        [Fact]
        public async Task NotFound_IsFinal()
            => Assert.Equal((HttpStatusCode.NotFound, 1), await SendAsync(HttpMethod.Get, Status(HttpStatusCode.NotFound)));

        [Fact]
        public async Task TransientFailures_OfAGet_AreRetried()
            => Assert.Equal((HttpStatusCode.OK, 3), await SendAsync(HttpMethod.Get,
                Status(HttpStatusCode.ServiceUnavailable), Status(HttpStatusCode.BadGateway), Status(HttpStatusCode.OK)));

        [Fact]
        public async Task TooManyRequests_IsRetriedAsRetryAfterSays()
            => Assert.Equal((HttpStatusCode.OK, 2), await SendAsync(HttpMethod.Get,
                Status(HttpStatusCode.TooManyRequests, "0"), Status(HttpStatusCode.OK)));

        [Fact]
        public async Task UnsafeMethods_AreNotRetried()
            => Assert.Equal((HttpStatusCode.ServiceUnavailable, 1), await SendAsync(HttpMethod.Post,
                Status(HttpStatusCode.ServiceUnavailable), Status(HttpStatusCode.OK)));

        [Fact]
        public async Task Retries_StopAfterThree()
            => Assert.Equal((HttpStatusCode.ServiceUnavailable, 4), await SendAsync(HttpMethod.Get,
                Status(HttpStatusCode.ServiceUnavailable)));
    }
}
