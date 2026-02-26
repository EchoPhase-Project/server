namespace EchoPhase.DAL.Postgres.Models
{
    public interface IConcurrentEntity
    {
        public Guid ConcurrencyStamp
        {
            get; set;
        }
    }
}
