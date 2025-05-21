using System.Text.Json.Serialization;

using EchoPhase.Interfaces;

namespace EchoPhase.Dtos
{
	public class TwitchApiPagination : ITwitchApiPagination
	{
		[JsonPropertyName("cursor")]
		public string? Cursor { get; set; } = string.Empty;
	}
}
