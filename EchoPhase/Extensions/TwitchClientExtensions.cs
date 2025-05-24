using EchoPhase.Dtos;
using EchoPhase.Interfaces;

namespace EchoPhase.Extensions
{
	public static class TwitchClientExtensions
	{
		public static ITwitchApiResponse<T> ToTwitchApiResponse<T>(
			this IClientResponse<ITwitchApiResponseDto<T>, TwitchApiError> response
		) where T : class => new TwitchApiResponse<T>(response);
	}
}
