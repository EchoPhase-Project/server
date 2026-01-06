namespace EchoPhase.DAL.Scylla
{
    public class PropertyBuilder<TEntity, TProp>
    {
        private readonly EntityBuilder<TEntity> _entityBuilder;
        private readonly string _propertyName;

        public PropertyBuilder(EntityBuilder<TEntity> entityBuilder, string propertyName)
        {
            _entityBuilder = entityBuilder;
            _propertyName = propertyName;
        }

        public PropertyBuilder<TEntity, TProp> HasColumnName(string columnName)
        {
            _entityBuilder.SetColumnName(_propertyName, columnName);
            return this;
        }
    }
}
