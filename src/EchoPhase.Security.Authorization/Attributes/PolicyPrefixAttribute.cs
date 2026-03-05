namespace EchoPhase.Security.Authorization.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PolicyPrefixAttribute : Attribute
    {
        public string Prefix { get; }

        public PolicyPrefixAttribute(string prefix)
        {
            Prefix = prefix;
        }
    }
}
