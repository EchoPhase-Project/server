namespace EchoPhase.DAL.Abstractions
{
    public interface IIdentifiable
    {
        Guid Id
        {
            get; set;
        }
    }
}
