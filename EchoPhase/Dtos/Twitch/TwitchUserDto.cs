namespace EchoPhase.Dtos
{
    public class TwitchUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string BroadcasterType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;
        public string OfflineImageUrl { get; set; } = string.Empty;
        public int ViewCount
        {
            get; set;
        }
        public string Email { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
    }
}
