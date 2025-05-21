namespace EchoPhase.Processors.Enums
{
	[Flags]
	public enum IntentsFlags : long
	{
		None = 0,
		Activity = 1 << 0,
		Login = 1 << 1,

		All = Activity | Login
	}
}
