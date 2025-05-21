using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;

namespace EchoPhase.Processors.Payloads
{
	public class AdjustPayload : IPayload
	{
		public long Intents { get; set; }

		public bool IsValid(out string errorMessage)
		{
			errorMessage = string.Empty;

			if (Intents < 0)
			{
				errorMessage = "Intents cannot be negative.";
				return false;
			}

			var allowed = (long)IntentsFlags.All;
			if ((Intents & ~allowed) != 0)
			{
				errorMessage = "Invalid or unallowed intents.";
				return false;
			}

			return true;
		}
	}
}
