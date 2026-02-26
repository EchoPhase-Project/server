namespace EchoPhase.DAL.Scylla.Interfaces
{
    public interface IMigration
    {
        string Id
        {
            get;
        }
        Task Up(Database db);
        Task Down(Database db);
        Task<bool> Validate(Database db);
    }
}
