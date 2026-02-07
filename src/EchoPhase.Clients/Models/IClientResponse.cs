using System.Net;

namespace EchoPhase.Clients.Models
{
    public interface IClientResponse<out TResponse, out TError>
    {
        public HttpStatusCode StatusCode
        {
            get;
        }
        public TError? Error
        {
            get;
        }
        public TResponse? Data
        {
            get;
        }
    }
}
