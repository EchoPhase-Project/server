using EchoPhase.Interfaces;

namespace EchoPhase.Processors.Payloads
{
	public class HandshakeAckPayload : IPayload
	{
		public double? HeartbeatInterval { get; set; }

		public bool IsValid(out string errorMessage)
		{
			errorMessage = string.Empty;

			if (HeartbeatInterval < 0)
			{
				errorMessage = "HeartbeatInterval cannot be negative.";
				return false;
			}

			return true;
		}
	}
}
