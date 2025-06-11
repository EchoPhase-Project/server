namespace EchoPhase.Interfaces
{
    public interface IConcurrentEntity
    {
        public Guid ConcurrencyStamp
        {
            get; set;
        }
    }
}
