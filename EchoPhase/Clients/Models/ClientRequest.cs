using EchoPhase.Interfaces;

namespace EchoPhase.Clients.Models
{
	public class ClientRequest<TQ, TB> : IClientRequest<TQ, TB> 
		where TQ : class
		where TB : class
	{
		public HttpMethod Method { get; set; } = HttpMethod.Get;
		public TQ? Query { get; set; } = default;
		public TB? Body { get; set; } = default;
	}
}
