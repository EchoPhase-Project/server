namespace EchoPhase.Attributes
{
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class QrFormatAttribute : Attribute
	{
		public string Format { get; }

		public QrFormatAttribute(string format)
		{
			Format = format.ToLowerInvariant();
		}
	}

}
