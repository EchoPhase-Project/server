using EchoPhase.Interfaces;

namespace EchoPhase.Processors.Payloads
{
	public class AdjustAckPayload : IPayload
	{
		public bool IsValid(out string errorMessage)
		{
			errorMessage = string.Empty;

			return true;
		}
	}
}
