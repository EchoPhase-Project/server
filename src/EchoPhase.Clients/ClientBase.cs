using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using EchoPhase.Clients.Helpers;
using EchoPhase.Clients.Models;

namespace EchoPhase.Clients
{
    public class ClientBase
    {
        private readonly HttpClient _client;
        private readonly QueryStringBuilder _queryStringBuilder;
        private readonly JsonSerializerOptions _options;

        public ClientBase(
            HttpClient client
        )
        {
            _client = client;
            _queryStringBuilder = new QueryStringBuilder();
            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        protected void Configure(Action<HttpClient> action)
        {
            action(_client);
        }

        protected Task ConfigureAsync(Func<HttpClient, Task> action)
        {
            return action(_client);
        }

        protected void WithAuth(string scheme, string token)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme, token);
        }

        protected async Task<IClientResponse<TR, TE>> SendAsync<TQ, TB, TR, TE>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body
        )
            where TQ : class
            where TB : class
            where TR : class
            where TE : class
        {
            var baseUri = _client.BaseAddress ?? throw new InvalidOperationException("HttpClient.BaseAddress is not set.");

            var fullUri = new Uri(baseUri, uri);
            UriBuilder uriBuilder = new UriBuilder(fullUri);

            if (query != null)
            {
                uriBuilder.Query = _queryStringBuilder.Build(query);
            }

            string url = uriBuilder.ToString();

            var httpRequest = new HttpRequestMessage(method, url);

            if (body != null)
            {
                var serializedBody = JsonSerializer.Serialize<TB>(body);
                httpRequest.Content = new StringContent(
                    serializedBody,
                    Encoding.UTF8,
                    MediaTypeNames.Application.Json
                );
            }

            var response = await _client.SendAsync(httpRequest);
            string responseString = await response.Content.ReadAsStringAsync();

            ClientResponse<TR, TE> apiResponse = new ClientResponse<TR, TE>()
            {
                StatusCode = response.StatusCode
            };

            if (response.IsSuccessStatusCode)
                apiResponse.Data = JsonSerializer.Deserialize<TR>(responseString, _options);
            else
                apiResponse.Error = JsonSerializer.Deserialize<TE>(responseString, _options);

            return apiResponse;
        }
    }
}
