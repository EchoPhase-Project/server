using System.Reflection;
using EchoPhase.Configuration.Settings;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla
{
    public class DbContext
    {
        private readonly IQueryProviderFactory _queryProviderFactory;
        private readonly Dictionary<Type, object> _entityBuilders = new();

        public Database Database
        {
            get;
        }

        public DbContext(IScyllaSettings options)
        {
            Database = new Database(options);
            _queryProviderFactory = new QueryProviderFactory(this);

            OnModelBuilding(Database.ModelBuilder);

            OnSetRegistration();
        }

        protected virtual void OnModelBuilding(ModelBuilder builder)
        {
        }

        public int SaveChanges() => Database.SaveChanges();
        public Task<int> SaveChangesAsync() => Database.SaveChangesAsync();

        public EntityBuilder<TEntity> Entity<TEntity>() where TEntity : class
        {
            if (!_entityBuilders.TryGetValue(typeof(TEntity), out var builderObj))
            {
                var builder = new EntityBuilder<TEntity>();
                _entityBuilders[typeof(TEntity)] = builder;
                return builder;
            }
            return (EntityBuilder<TEntity>)builderObj;
        }

        public DbSet<TEntity> Set<TEntity>() where TEntity : class, new()
            => new DbSet<TEntity>(_queryProviderFactory);

        public IQueryable<T> Query<T>() where T : class, new()
        {
            return Set<T>().AsQueryable();
        }

        public virtual void OnSetRegistration()
        {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.IsGenericType &&
                            p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));

            foreach (var prop in properties)
            {
                var entityType = prop.PropertyType.GetGenericArguments()[0];

                var set = Activator.CreateInstance(
                    typeof(DbSet<>).MakeGenericType(entityType),
                    _queryProviderFactory
                ) ?? throw new InvalidOperationException(
                    $"Failed to create DbSet<{entityType.Name}>. Ensure a matching constructor exists."
                );

                prop.SetValue(this, set);
            }
        }
    }
}
