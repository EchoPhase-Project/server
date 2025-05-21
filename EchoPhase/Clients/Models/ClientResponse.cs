using System.Net;

using EchoPhase.Interfaces;

namespace EchoPhase.Clients.Models
{
	public class ClientResponse<TResponse, TError> : IClientResponse<TResponse, TError>
	{
		public HttpStatusCode StatusCode { get; init; }
		public TResponse? Data { get; set; } = default;
		public TError? Error { get; set; } = default;
	}
}
