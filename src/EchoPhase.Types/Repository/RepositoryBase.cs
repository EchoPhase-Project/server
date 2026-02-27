using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace EchoPhase.Types.Repository
{
    public abstract class RepositoryBase<TEntity, TO> : IRepositoryBase<TEntity, TO>
        where TO : class, new()
        where TEntity : class
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propsCache = new();

        protected TO _options;

        public RepositoryBase()
        {
            _options = new();
        }

        public RepositoryBase(TO options)
        {
            _options = options;
        }

        public void WithOptions(TO options)
        {
            _options = options;
        }

        public void WithOptions(Action<TO> configure)
        {
            configure(_options);
        }

        protected IQueryable<T> ApplySearchOptions<T, TOptions>(
            IQueryable<T> query,
            TOptions options,
            Func<IQueryable<T>, TOptions, IQueryable<T>>? extraFilters = null
        )
        {
            var props = _propsCache.GetOrAdd(typeof(TOptions), t => t.GetProperties());
            var parameter = Expression.Parameter(typeof(T), "x");
            Expression? predicate = null;

            foreach (var prop in props)
            {
                var value = prop.GetValue(options);
                if (value is null) continue;

                var targetProp = typeof(T).GetProperty(prop.Name);
                if (targetProp is null) continue;

                var left = Expression.Property(parameter, targetProp);
                Expression right;

                if (targetProp.PropertyType == prop.PropertyType)
                {
                    right = Expression.Constant(value, prop.PropertyType);
                }
                else if (targetProp.PropertyType.IsAssignableFrom(prop.PropertyType))
                {
                    right = Expression.Convert(
                        Expression.Constant(value, prop.PropertyType),
                        targetProp.PropertyType
                    );
                }
                else
                {
                    var underlyingType = Nullable.GetUnderlyingType(targetProp.PropertyType)
                        ?? targetProp.PropertyType;
                    try
                    {
                        var converted = Convert.ChangeType(value, underlyingType);
                        right = Expression.Constant(converted, targetProp.PropertyType);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Cannot convert property '{prop.Name}' from '{prop.PropertyType}' to '{targetProp.PropertyType}'.", ex);
                    }
                }

                var comparison = Expression.Equal(left, right);
                predicate = predicate is null ? comparison : Expression.AndAlso(predicate, comparison);
            }

            if (predicate is not null)
            {
                var lambda = Expression.Lambda<Func<T, bool>>(predicate, parameter);
                query = query.Where(lambda);
            }

            if (extraFilters is not null)
                query = extraFilters(query, options);

            return query;
        }

        protected IQueryable<T> ApplySearchOptions<T, TOptions>(
            IQueryable<T> query,
            Action<TOptions> configure,
            Func<IQueryable<T>, TOptions, IQueryable<T>>? extraFilters = null
        )
            where TOptions : class, new()
        {
            var options = new TOptions();
            configure(options);
            return ApplySearchOptions(query, options, extraFilters);
        }

        public abstract IQueryable<TEntity> Build();
    }
}
