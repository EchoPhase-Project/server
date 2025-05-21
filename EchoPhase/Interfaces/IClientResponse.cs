using System.Net;

namespace EchoPhase.Interfaces
{
	public interface IClientResponse<TResponse, TError>
	{
		public HttpStatusCode StatusCode { get; init; }
		public TError? Error { get; set; }
		public TResponse? Data { get; set; }
	}
}
