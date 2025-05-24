namespace EchoPhase.Interfaces
{
	public interface IClientRequest<out TQ, out TB>
		where TQ : class
		where TB : class
	{
		public HttpMethod Method { get; }
		public TQ? Query { get; }
		public TB? Body { get; }
	}
}
