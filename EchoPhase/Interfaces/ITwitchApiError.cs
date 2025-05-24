namespace EchoPhase.Interfaces
{
	public interface ITwitchApiError
	{
		public string Error { get; }
		public int Status { get; }
		public string Message { get; }
	}
}
