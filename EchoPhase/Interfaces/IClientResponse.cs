using System.Net;

namespace EchoPhase.Interfaces
{
	public interface IClientResponse<out TResponse, out TError>
	{
		public HttpStatusCode StatusCode { get; }
		public TError? Error { get; }
		public TResponse? Data { get; }
	}
}
