using EchoPhase.Interfaces;
using EchoPhase.Extensions;

namespace EchoPhase.Clients
{
	public class DiscordClientBase : ClientBase
	{
		private readonly HttpClient _client;
		private readonly ILogger _logger;

		public DiscordClientBase(
			HttpClient client, 
			ILogger logger
		) : base(client)
		{
			_client = client;
			_logger = logger;
		}

		protected async Task<IDiscordApiResponse<TResponse>> SendAsync<TQuery, TBody, TResponse>(
			string uri, 
			HttpMethod method, 
			TQuery? query, 
			TBody? body
		)
		{
			_logger.LogInformation("Requested DiscordAPI {URI}", uri);

			return (await base.SendAsync<TQuery, TBody, TResponse, IDiscordApiError>(
				uri, 
				method, 
				query, 
				body
			)).ToDiscordApiResponse();
		}
	}
}
