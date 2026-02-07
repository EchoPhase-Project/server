namespace EchoPhase.DAL.Scylla.Interfaces
{
    public interface ICqlGenerator
    {
        (string cql, object[] parameters) GenerateInsert(object entity, IEntityBuilder builder);
        (string cql, object[] parameters) GenerateUpdate(object entity, IEntityBuilder builder);
        (string cql, object[] parameters) GenerateDelete(object entity, IEntityBuilder builder);
    }
}
