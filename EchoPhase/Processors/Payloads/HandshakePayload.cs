using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;

namespace EchoPhase.Processors.Payloads
{
	public class HandshakePayload : IPayload
	{
		public long? Intents { get; set; }

		public bool IsValid(out string errorMessage)
		{
			errorMessage = string.Empty;

			if (Intents == null)
				return true;

			if (Intents < 0)
			{
				errorMessage = "Intents cannot be negative.";
				return false;
			}

			var allowed = (long)IntentsFlags.All;
			if ((Intents.Value & ~allowed) != 0)
			{
				errorMessage = "Invalid or unallowed intents.";
				return false;
			}

			return true;
		}
	}
}
