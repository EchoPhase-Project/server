namespace EchoPhase.Clients.Helpers
{
    public abstract class DataBuilderBase<TBuilder> : IDataBuilder<TBuilder>
        where TBuilder : DataBuilderBase<TBuilder>, new()
    {
        protected IBuildOptions _options = new BuildOptions();

        public DataBuilderBase()
        {
        }

        public TBuilder WithOptions(IBuildOptions options)
        {
            _options = options;
            return (TBuilder)this;
        }

        public TBuilder WithOptions(Action<IBuildOptions> configure)
        {
            configure(_options);
            return (TBuilder)this;
        }

        public virtual TBuilder Clone()
        {
            return new TBuilder()
                .WithOptions(_options);
        }

        public abstract object Build(object? obj);

        protected static bool IsSimple(Type type)
        {
            return
                type.IsPrimitive ||
                type.IsEnum ||
                type == typeof(string) ||
                type == typeof(decimal) ||
                type == typeof(DateTime) ||
                type == typeof(Guid) ||
                type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan);
        }

        protected bool IsEnumerable(Type type)
        {
            if (type == typeof(string))
                return false;

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
                return true;

            return type.GetInterfaces()
                       .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IEnumerable<>));
        }
    }
}
