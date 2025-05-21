using EchoPhase.Interfaces;

namespace EchoPhase.Processors.Payloads
{
	public class PongPayload : IPayload
	{
		public bool IsValid(out string errorMessage)
		{
			errorMessage = string.Empty;

			return true;
		}
	}
}
