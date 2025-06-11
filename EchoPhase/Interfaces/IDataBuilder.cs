namespace EchoPhase.Interfaces
{
    public interface IDataBuilder<TBuilder>
    {
        public TBuilder Clone();
        public object Build(object? obj);
    }
}
