using EchoPhase.Clients.Models;

namespace EchoPhase.Clients.Twitch.Models
{
    public class TwitchApiResponse<T> : ClientResponse<ITwitchApiResponseDto<T>, ITwitchApiError>, ITwitchApiResponse<T> where T : class
    {
        public TwitchApiResponse(IClientResponse<ITwitchApiResponseDto<T>, ITwitchApiError> inner)
        {
            this.Data = inner.Data;
            this.Error = inner.Error;
            this.StatusCode = inner.StatusCode;
        }
    }
}
