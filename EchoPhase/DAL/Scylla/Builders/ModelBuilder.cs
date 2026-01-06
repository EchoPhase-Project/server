namespace EchoPhase.DAL.Scylla
{
    public class ModelBuilder
    {
        private readonly Dictionary<Type, object> _configs = new();

        public EntityBuilder<TEntity> Entity<TEntity>()
            where TEntity : class
        {
            if (!_configs.TryGetValue(typeof(TEntity), out var existing))
            {
                var builder = new EntityBuilder<TEntity>();
                _configs[typeof(TEntity)] = builder;
                return builder;
            }
            return (EntityBuilder<TEntity>)existing;
        }

        internal EntityBuilder<TEntity>? GetBuilder<TEntity>() where TEntity : class
            => _configs.TryGetValue(typeof(TEntity), out var b) ? (EntityBuilder<TEntity>)b : null;
    }
}
