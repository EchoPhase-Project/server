using System.Text;
using System.Text.Json;

using EchoPhase.Interfaces;
using EchoPhase.Clients.Models;
using EchoPhase.Helpers.Builders;

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

		protected async Task<IClientResponse<TResponse, TError>> SendAsync<TRequestQuery, TRequestBody, TResponse, TError>(
			string uri, 
			HttpMethod method,
			TRequestQuery? query, 
			TRequestBody? body
		)
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

			Console.WriteLine($"Requested API {url} {baseUri.ToString()}");
			
			if (body != null)
			{
				var serializedBody = JsonSerializer.Serialize(body);
				httpRequest.Content = new StringContent(serializedBody, Encoding.UTF8, "application/json");
			}

			var response = await _client.SendAsync(httpRequest);
			string responseString = await response.Content.ReadAsStringAsync();
			IClientResponse<TResponse, TError> apiResponse = new ClientResponse<TResponse, TError>()
			{
				StatusCode = response.StatusCode
			};

			if (response.IsSuccessStatusCode)
				apiResponse.Data = JsonSerializer.Deserialize<TResponse>(responseString, _options);
			else
				apiResponse.Error = JsonSerializer.Deserialize<TError>(responseString, _options);

            return apiResponse;
		}
	}
}
