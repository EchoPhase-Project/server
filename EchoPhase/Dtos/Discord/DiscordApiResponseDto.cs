using System.Text.Json.Serialization;

using EchoPhase.Interfaces;

namespace EchoPhase.Dtos
{
		public class DiscordApiResponseDto<T> : IDiscordApiResponseDto<T>
		{
			[JsonPropertyName("data")]
			public T? Data { get; set; }

			[JsonPropertyName("message")]
			public string? Message { get; set; }
		}
}
