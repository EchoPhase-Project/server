using EchoPhase.Interfaces;

namespace EchoPhase.Processors.Payloads
{
	public class DisconnectAckPayload : IPayload
	{
		public bool IsValid(out string errorMessage)
		{
			errorMessage = string.Empty;

			return true;
		}
	}
}
