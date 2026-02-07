namespace EchoPhase.Clients.Twitch.Models
{
    public interface ITwitchApiPagination
    {
        public string? Cursor
        {
            get; set;
        }
    }
}
