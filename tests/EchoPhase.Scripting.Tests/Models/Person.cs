namespace EchoPhase.Scripting.Tests.Models
{
    public class Person
    {
        public string Name { get; set; } = "";
        public int Age
        {
            get; set;
        }
        public Address Address { get; set; } = new();
    }
}
