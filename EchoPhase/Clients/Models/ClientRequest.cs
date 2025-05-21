using EchoPhase.Interfaces;

namespace EchoPhase.Clients.Models
{
	public class ClientRequest<TQuery, TBody> : IClientRequest<TQuery, TBody>
	{
		public required HttpMethod Method { get; init; }

		public required TQuery? Query { get; init; }
		public required TBody? Body { get; init; }
	}
}
