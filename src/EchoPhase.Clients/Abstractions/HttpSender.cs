// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using EchoPhase.Clients.Helpers;

namespace EchoPhase.Clients.Abstractions
{
    public sealed class HttpSender
    {
        private readonly HttpClient _client;
        private readonly QueryStringBuilder _queryStringBuilder;

        private static readonly JsonSerializerOptions _jsonOptions =
            new(JsonSerializerDefaults.Web);

        public HttpSender(HttpClient client)
        {
            _client = client;
            _queryStringBuilder = new QueryStringBuilder();
        }

        public async Task<(HttpStatusCode StatusCode, Stream Stream)> SendAsync<TQ, TB>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body,
            AuthenticationHeaderValue? auth = null,
            CancellationToken ct = default)
            where TQ : class
            where TB : class
        {
            var baseUri = _client.BaseAddress
                ?? throw new InvalidOperationException("HttpClient.BaseAddress is not set.");

            var url = query != null
                ? new UriBuilder(new Uri(baseUri, uri))
                { Query = _queryStringBuilder.Build(query) }.ToString()
                : new Uri(baseUri, uri).ToString();

            using var request = new HttpRequestMessage(method, url);

            if (auth != null)
                request.Headers.Authorization = auth;

            if (body != null)
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body, _jsonOptions),
                    Encoding.UTF8,
                    MediaTypeNames.Application.Json);

            var response = await _client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);

            var stream = await response.Content.ReadAsStreamAsync(ct);
            return (response.StatusCode, stream);
        }
    }
}
