// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla.Builders
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

        internal IEntityBuilder? GetBuilder(Type entityType)
            => _configs.TryGetValue(entityType, out var b) ? (IEntityBuilder)b : null;

        internal IReadOnlyCollection<IEntityBuilder> GetAllBuilders()
            => _configs.Values.Cast<IEntityBuilder>().ToList();
    }
}
