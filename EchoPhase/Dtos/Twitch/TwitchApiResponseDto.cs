using System.Text.Json.Serialization;

using EchoPhase.Interfaces;

namespace EchoPhase.Dtos
{
	public class TwitchApiResponseDto<TResponse> : ITwitchApiResponseDto<TResponse>
	{
		[JsonPropertyName("data")]
		public TResponse? Data { get; set; } = default;

		[JsonPropertyName("pagination")]
		public ITwitchApiPagination? Pagination { get; set; } = default;
	}
}
