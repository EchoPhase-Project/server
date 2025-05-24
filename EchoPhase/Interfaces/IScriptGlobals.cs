namespace EchoPhase.Interfaces
{
	public interface IScriptGlobals<out T>
	{
		public T Payload { get; }
		public IScriptContext Context { get; }
	}
}
