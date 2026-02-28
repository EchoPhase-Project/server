namespace EchoPhase.DAL.Abstractions
{
    public interface IConcurrentEntity
    {
        public Guid ConcurrencyStamp
        {
            get; set;
        }
    }
}
