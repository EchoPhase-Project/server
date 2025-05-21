namespace EchoPhase.Interfaces
{
	public interface IPayload
	{
		public bool IsValid(out string errorMessage);
	}
}
