using EchoPhase.Processors.Enums;

namespace EchoPhase.Extensions
{
	public static class IntentsExtensions
	{
		public static bool HasIntents(this IntentsFlags current, IntentsFlags targetIntents)
		{
			return (current & targetIntents) != 0;
		}

		public static bool HasIntents(this IntentsFlags current, long targetIntents)
		{
			return ((long)current & targetIntents) != 0;
		}

		public static bool HasIntents(this long current, long targetIntents)
		{
			return (current & targetIntents) != 0;
		}

		public static bool HasIntents(this long? current, long targetIntents)
		{
			if (current is null)
				return false;
			return HasIntents(current.Value, targetIntents);
		}
	}
}
