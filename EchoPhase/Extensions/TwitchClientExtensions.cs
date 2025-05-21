using EchoPhase.Interfaces;
using EchoPhase.Dtos;

namespace EchoPhase.Extensions
{
	public static class TwitchClientExtensions
	{
		public static ITwitchApiResponse<T> ToTwitchApiResponse<T>(
			this IClientResponse<ITwitchApiResponseDto<T>, ITwitchApiError> response)
		{
			return new TwitchApiResponse<T>(response);
		}
	}
}
