using System.Linq.Expressions;

using EchoPhase.Interfaces;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public abstract class RepositoryBase<TO> : IRepositoryBase<TO>
        where TO : class, new()
    {
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
            var parameter = Expression.Parameter(typeof(T), "x");
            Expression? predicate = null;

            foreach (var prop in typeof(TOptions).GetProperties())
            {
                var value = prop.GetValue(options);
                if (value == null) continue;

                var targetProp = typeof(T).GetProperty(prop.Name);
                if (targetProp == null) continue;

                var left = Expression.Property(parameter, targetProp);
                Expression right = Expression.Constant(value, prop.PropertyType);
                if (targetProp.PropertyType != prop.PropertyType)
                {
                    if (targetProp.PropertyType.IsAssignableFrom(prop.PropertyType))
                    {
                        right = Expression.Convert(right, targetProp.PropertyType);
                    }
                    else
                    {
                        object? typedValue = null;
                        try
                        {
                            typedValue = Convert.ChangeType(value, Nullable.GetUnderlyingType(targetProp.PropertyType) ?? targetProp.PropertyType);
                        }
                        catch
                        {
                            continue;
                        }

                        right = Expression.Constant(typedValue, targetProp.PropertyType);
                    }
                }

                var comparison = Expression.Equal(left, right);
                predicate = predicate == null ? comparison : Expression.AndAlso(predicate, comparison);
            }

            if (predicate != null)
            {
                var lambda = Expression.Lambda<Func<T, bool>>(predicate, parameter);
                query = query.Where(lambda);
            }

            if (extraFilters != null)
            {
                query = extraFilters(ApplySearchOptions<T, TOptions>(query, options), options);
            }

            return query;
        }

        protected IQueryable<T> ApplySearchOptions<T, TOptions>(
            IQueryable<T> query,
            Action<TOptions> configure,
            Func<IQueryable<T>, TOptions, IQueryable<T>>? extraFilters = null
        )
            where TOptions : class, new()
        {
            TOptions options = new();
            configure(options);
            return ApplySearchOptions(query, options, extraFilters);
        }

        public abstract IQueryable Build();
    }
}
