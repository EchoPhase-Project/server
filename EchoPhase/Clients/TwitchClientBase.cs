using EchoPhase.Dtos;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;

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

        protected async Task<ITwitchApiResponse<TR>> SendAsync<TQ, TB, TR>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body
        )
            where TQ : class
            where TB : class
            where TR : class
        {
            _logger.LogInformation("Requested TwitchAPI {URI}", uri);

            return (await base.SendAsync<TQ, TB, TwitchApiResponseDto<TR>, TwitchApiError>(
                uri,
                method,
                query,
                body
            ))
                .ToTwitchApiResponse();
        }
    }
}
