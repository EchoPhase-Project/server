namespace EchoPhase.DAL.Postgres.Models
{
    public interface ITrackingEntity
    {
        DateTime UpdatedAt
        {
            get; set;
        }
        DateTime CreatedAt
        {
            get; set;
        }
    }
}
