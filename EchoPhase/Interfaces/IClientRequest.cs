namespace EchoPhase.Interfaces
{
	public interface IClientRequest<TQuery, TBody>
	{
		public HttpMethod Method { get; init; }

		public TQuery? Query { get; init; }
		public TBody? Body { get; init; }
	}
}
