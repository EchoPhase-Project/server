namespace EchoPhase.QRCodes.Generators
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class QRFormatAttribute : Attribute
    {
        public string Format
        {
            get;
        }

        public QRFormatAttribute(string format)
        {
            Format = format.ToLowerInvariant();
        }
    }

}
