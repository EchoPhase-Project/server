// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Builders
{
    public class PropertyBuilder<TEntity, TProp> where TEntity : class
    {
        private readonly EntityBuilder<TEntity> _entityBuilder;
        private readonly string _propertyName;

        internal PropertyBuilder(EntityBuilder<TEntity> entityBuilder, string propertyName)
        {
            _entityBuilder = entityBuilder;
            _propertyName = propertyName;
        }

        public PropertyBuilder<TEntity, TProp> HasColumnName(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentException("Column name cannot be null or empty", nameof(columnName));

            _entityBuilder.SetColumnName(_propertyName, columnName);
            return this;
        }

        public PropertyBuilder<TEntity, TProp> HasColumnType(Type type)
        {
            _entityBuilder.SetColumnType(_propertyName, type);
            return this;
        }

        public PropertyBuilder<TEntity, TProp> HasColumnType<TColumnType>()
        {
            _entityBuilder.SetColumnType(_propertyName, typeof(TColumnType));
            return this;
        }

        public EntityBuilder<TEntity> And() => _entityBuilder;
    }
}
