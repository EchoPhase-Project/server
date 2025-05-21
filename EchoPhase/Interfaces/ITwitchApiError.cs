namespace EchoPhase.Interfaces
{
	public interface ITwitchApiError
	{
		string Error { get; }
		int Status { get; }
		string Message { get; }
	}
}
