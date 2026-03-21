namespace EchoPhase.Clients.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ProviderNameAttribute : Attribute
    {
        public string Name { get; }

        public ProviderNameAttribute(string name)
        {
            Name = name;
        }
    }
}
