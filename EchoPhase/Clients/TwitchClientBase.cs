using EchoPhase.Interfaces;
using EchoPhase.Extensions;

namespace EchoPhase.Clients
{
	public class TwitchClientBase : ClientBase
	{
		private readonly HttpClient _client;
		private readonly ILogger _logger;

		public TwitchClientBase(
			HttpClient client, 
			ILogger logger
		) : base(client)
		{
			_client = client;
			_logger = logger;
		}

		protected async Task<ITwitchApiResponse<TResponse>> SendAsync<TQuery, TBody, TResponse>(
			string uri, 
			HttpMethod method, 
			TQuery? query, 
			TBody? body
		)
		{
			_logger.LogInformation("Requested TwitchAPI {URI}", uri);

			return (await base.SendAsync<TQuery, TBody, ITwitchApiResponseDto<TResponse>, ITwitchApiError>(
				uri, 
				method, 
				query, 
				body
			)).ToTwitchApiResponse();
		}
	}
}
