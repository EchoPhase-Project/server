using EchoPhase.Dtos;
using EchoPhase.Interfaces;

namespace EchoPhase.Extensions
{
	public static class DiscordClientExtensions
	{
		public static IDiscordApiResponse<T> ToDiscordApiResponse<T>(
			this IClientResponse<T, IDiscordApiError> response
		) where T : class => new DiscordApiResponse<T>(response);
	}
}
