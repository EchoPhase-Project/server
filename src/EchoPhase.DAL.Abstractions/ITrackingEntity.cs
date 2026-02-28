namespace EchoPhase.DAL.Abstractions
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
