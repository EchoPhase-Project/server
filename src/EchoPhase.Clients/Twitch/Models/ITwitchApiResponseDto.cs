namespace EchoPhase.Clients.Twitch.Models
{
    public interface ITwitchApiResponseDto<out T>
    {
        public T? Data
        {
            get;
        }
        public ITwitchApiPagination? Pagination
        {
            get;
        }
    }
}
