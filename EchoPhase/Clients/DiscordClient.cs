using EchoPhase.Dtos;
using EchoPhase.Interfaces;

namespace EchoPhase.Clients
{
	public class DiscordClient : DiscordClientBase
	{
		private readonly HttpClient _client;
		private readonly ILogger<DiscordClient> _logger;

		public DiscordClient(
			HttpClient client, ILogger<DiscordClient> logger
		) : base(client, logger)
		{
			_client = client;
			_logger = logger;
		}

		public async Task<IDiscordApiResponse<List<DiscordGuildResponseDto>>> GetUserGuildsAsync(DiscordUserGuildsQueryDto query) =>
			await SendAsync<DiscordUserGuildsQueryDto, object, List<DiscordGuildResponseDto>>(
				"users/@me/guilds",
				HttpMethod.Get,
				query,
				null
			);
	}
}
