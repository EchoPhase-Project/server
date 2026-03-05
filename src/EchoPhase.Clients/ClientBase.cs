using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using EchoPhase.Clients.Helpers;
using EchoPhase.Clients.Models;

namespace EchoPhase.Clients
{
    public abstract class ClientBase
    {
        private readonly HttpClient _client;
        private readonly QueryStringBuilder _queryStringBuilder;
        private static readonly JsonSerializerOptions _jsonOptions =
            new(JsonSerializerDefaults.Web);

        protected ClientBase(HttpClient client)
        {
            _client = client;
            _queryStringBuilder = new QueryStringBuilder();
        }
        protected AuthenticationHeaderValue WithAuth(string scheme, string token) =>
            new AuthenticationHeaderValue(scheme, token);

        protected async Task<IClientResponse<TR, TE>> SendAsync<TQ, TB, TR, TE>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body,
            AuthenticationHeaderValue? auth = null,
            CancellationToken ct = default)
            where TQ : class
            where TB : class
            where TR : class
            where TE : class
        {
            var baseUri = _client.BaseAddress
                ?? throw new InvalidOperationException("HttpClient.BaseAddress is not set.");

            string url = query != null
                ? new UriBuilder(new Uri(baseUri, uri)) { Query = _queryStringBuilder.Build(query) }.ToString()
                : new Uri(baseUri, uri).ToString();

            using var httpRequest = new HttpRequestMessage(method, url);

            if (auth != null)
                httpRequest.Headers.Authorization = auth;

            if (body != null)
            {
                httpRequest.Content = new StringContent(
                    JsonSerializer.Serialize(body, _jsonOptions),
                    Encoding.UTF8,
                    MediaTypeNames.Application.Json);
            }

            using var response = await _client.SendAsync(httpRequest, ct);
            var apiResponse = new ClientResponse<TR, TE> { StatusCode = response.StatusCode };

            await using var stream = await response.Content.ReadAsStreamAsync(ct);

            if (response.IsSuccessStatusCode)
                apiResponse.Data = await JsonSerializer.DeserializeAsync<TR>(stream, _jsonOptions, ct);
            else
                apiResponse.Error = await JsonSerializer.DeserializeAsync<TE>(stream, _jsonOptions, ct);

            return apiResponse;
        }
    }
}
