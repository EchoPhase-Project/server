using EchoPhase.Clients.Models;
using EchoPhase.Interfaces;

namespace EchoPhase.Dtos
{
    public class DiscordApiResponse<T> : ClientResponse<T, IDiscordApiError>, IDiscordApiResponse<T> where T : class
    {
        public DiscordApiResponse(IClientResponse<T, IDiscordApiError> inner)
        {
            this.Data = inner.Data;
            this.Error = inner.Error;
            this.StatusCode = inner.StatusCode;
        }
    }
}
