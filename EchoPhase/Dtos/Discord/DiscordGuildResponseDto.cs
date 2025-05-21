using System.Text.Json.Serialization;

namespace EchoPhase.Dtos
{
	public class DiscordGuildResponseDto
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = null!;

		[JsonPropertyName("name")]
		public string Name { get; set; } = null!;

		[JsonPropertyName("icon")]
		public string? Icon { get; set; }

		[JsonPropertyName("banner")]
		public string? Banner { get; set; }

		[JsonPropertyName("owner")]
		public bool Owner { get; set; }

		[JsonPropertyName("permissions")]
		public string Permissions { get; set; } = null!;

		[JsonPropertyName("features")]
		public List<string> Features { get; set; } = new();

		[JsonPropertyName("approximate_member_count")]
		public int ApproximateMemberCount { get; set; }

		[JsonPropertyName("approximate_presence_count")]
		public int ApproximatePresenceCount { get; set; }
	}
}
