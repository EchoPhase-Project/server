using EchoPhase.Interfaces;
using EchoPhase.Dtos;

namespace EchoPhase.Extensions
{
	public static class DiscordClientExtensions
	{
		public static IDiscordApiResponse<T> ToDiscordApiResponse<T>(
			this IClientResponse<T, IDiscordApiError> response)
		{
			return new DiscordApiResponse<T>(response);
		}
	}
}
