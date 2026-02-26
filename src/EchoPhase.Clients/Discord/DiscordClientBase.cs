using EchoPhase.Clients.Discord.Extensions;
using EchoPhase.Clients.Discord.Models;
using Microsoft.Extensions.Logging;

namespace EchoPhase.Clients.Discord
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

        public void WithAuth(string token)
        {
            base.WithAuth("Bot", token);
        }

        protected async Task<IDiscordApiResponse<TR>> SendAsync<TQ, TB, TR>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body
        )
            where TQ : class
            where TB : class
            where TR : class
        {
            _logger.LogInformation("Requested DiscordAPI {URI}", uri);

            return (await base.SendAsync<TQ, TB, TR, DiscordApiError>(
                uri,
                method,
                query,
                body
            ))
                .ToDiscordApiResponse();
        }
    }
}
