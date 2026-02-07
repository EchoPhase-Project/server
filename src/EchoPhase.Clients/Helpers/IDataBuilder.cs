namespace EchoPhase.Clients.Helpers
{
    public interface IDataBuilder<TBuilder>
    {
        public TBuilder Clone();
        public object Build(object? obj);
    }
}
