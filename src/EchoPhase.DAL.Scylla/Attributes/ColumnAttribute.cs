namespace EchoPhase.DAL.Scylla.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        public string Name
        {
            get;
        }
        public ColumnAttribute(string name) => Name = name;
    }
}
