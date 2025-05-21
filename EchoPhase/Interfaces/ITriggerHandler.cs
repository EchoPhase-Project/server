namespace EchoPhase.Interfaces
{
	public interface ITriggerHandler
	{
		Task<string> HandleAsync(string input);
	}
}
