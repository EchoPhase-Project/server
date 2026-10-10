// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Reflection;
using EchoPhase.Configuration.Database.Scylla;
using EchoPhase.DAL.Scylla.Builders;
using EchoPhase.DAL.Scylla.Interfaces;
using EchoPhase.DAL.Scylla.Linq;
using EchoPhase.DAL.Scylla.Schema;

namespace EchoPhase.DAL.Scylla.Database
{
    /// <summary>
    /// Base context. Cheap to create: give it the application's shared
    /// <see cref="ScyllaConnection"/> and scope it per unit of work.
    /// </summary>
    /// <remarks>
    /// The model is built once per context type (like EF) by calling
    /// <see cref="OnModelBuilding"/> on the first instance, so it must not
    /// depend on per-instance state.
    /// </remarks>
    public class DbContext : IDisposable
    {
        private static readonly ConcurrentDictionary<Type, ModelBuilder> Models = new();
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> SetProperties = new();

        private readonly IQueryProviderFactory _queryProviderFactory;

        public Database Database
        {
            get;
        }

        /// <summary>
        /// Uses a shared connection; disposing the context leaves it open.
        /// </summary>
        public DbContext(ScyllaConnection connection)
        {
            Database = new Database(connection, GetOrBuildModel(), ownsConnection: false);
            _queryProviderFactory = new QueryProviderFactory(this);
            OnSetRegistration();
        }

        /// <summary>
        /// Standalone use: opens and owns a connection of its own.
        /// </summary>
        public DbContext(IScyllaOptions options)
        {
            Database = new Database(new ScyllaConnection(options), GetOrBuildModel(), ownsConnection: true);
            _queryProviderFactory = new QueryProviderFactory(this);
            OnSetRegistration();
        }

        private ModelBuilder GetOrBuildModel()
            => Models.GetOrAdd(GetType(), _ =>
            {
                var builder = new ModelBuilder();
                OnModelBuilding(builder);

                // Fail when the context is first created, not on the first query.
                foreach (var entity in builder.GetAllBuilders())
                    entity.Validate();

                return builder;
            });

        protected virtual void OnModelBuilding(ModelBuilder builder)
        {
        }

        public int SaveChanges() => Database.SaveChanges();

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Database.SaveChangesAsync(ct);

        /// <summary>
        /// Model entry for <typeparamref name="TEntity"/>. Configure entities in
        /// <see cref="OnModelBuilding"/>: the model is shared by every context of
        /// this type, so changes made here affect all of them.
        /// </summary>
        public EntityBuilder<TEntity> Entity<TEntity>() where TEntity : class
            => Database.ModelBuilder.Entity<TEntity>();

        public DbSet<TEntity> Set<TEntity>() where TEntity : class, new()
            => new DbSet<TEntity>(_queryProviderFactory);

        public IQueryable<T> Query<T>() where T : class, new()
        {
            return Set<T>().AsQueryable();
        }

        /// <inheritdoc cref="Database.ValidateSchemaAsync"/>
        public Task<IReadOnlyList<SchemaProblem>> ValidateSchemaAsync(CancellationToken ct = default)
            => Database.ValidateSchemaAsync(ct);

        public virtual void OnSetRegistration()
        {
            var properties = SetProperties.GetOrAdd(GetType(), type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.IsGenericType &&
                            p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                .ToArray());

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

        public void Dispose()
        {
            Database.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
