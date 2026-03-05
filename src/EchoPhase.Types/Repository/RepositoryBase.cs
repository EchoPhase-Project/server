using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using EchoPhase.DAL.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.Types.Repository
{
    public abstract class RepositoryBase<TEntity, TOptions, TSearchOptions> : IRepositoryBase<TEntity, TOptions>
        where TOptions      : class, new()
        where TSearchOptions : class, new()
        where TEntity       : class, ITrackingEntity, IIdentifiable
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propsCache = new();

        protected TOptions _options;

        public RepositoryBase() => _options = new();
        public RepositoryBase(TOptions options) => _options = options;

        public void WithOptions(TOptions options) => _options = options;
        public void WithOptions(Action<TOptions> configure) => configure(_options);

        // ── Get ───────────────────────────────────────────────────────────────

        public CursorPage<TEntity> Get(
            TSearchOptions options,
            CursorOptions? cursor = null,
            Func<IQueryable<TEntity>, TSearchOptions, IQueryable<TEntity>>? extraFilters = null)
        {
            var query = ApplySearchOptions<TEntity, TSearchOptions>(
                Build(), options, (q, opts) =>
                {
                    q = ApplyExtraFilters(q, opts);
                    if (extraFilters is not null)
                        q = extraFilters(q, opts);
                    return q;
                });

            if (cursor is not null)
                return ApplyCursor(query, cursor);

            return new CursorPage<TEntity> { Data = query };
        }

        public CursorPage<TEntity> Get(
            Action<TSearchOptions> configure,
            CursorOptions? cursor = null,
            Func<IQueryable<TEntity>, TSearchOptions, IQueryable<TEntity>>? extraFilters = null)
        {
            var options = new TSearchOptions();
            configure(options);
            return Get(options, cursor, extraFilters);
        }

        public CursorPage<TEntity> Get(
            TSearchOptions options,
            Action<CursorOptions> configureCursor,
            Func<IQueryable<TEntity>, TSearchOptions, IQueryable<TEntity>>? extraFilters = null)
        {
            var cursor = new CursorOptions();
            configureCursor(cursor);
            return Get(options, cursor, extraFilters);
        }

        public CursorPage<TEntity> Get(
            Action<TSearchOptions> configure,
            Action<CursorOptions> configureCursor,
            Func<IQueryable<TEntity>, TSearchOptions, IQueryable<TEntity>>? extraFilters = null)
        {
            var options = new TSearchOptions();
            configure(options);
            var cursor = new CursorOptions();
            configureCursor(cursor);
            return Get(options, cursor, extraFilters);
        }

        // ── Abstracts ─────────────────────────────────────────────────────────

        public abstract IQueryable<TEntity> Build();

        protected virtual IQueryable<TEntity> ApplyExtraFilters(
            IQueryable<TEntity> query,
            TSearchOptions options) => query;

        // ── ApplySearchOptions / ApplyCursor ──────────────────────────────────

        protected IQueryable<T> ApplySearchOptions<T, TOpts>(
            IQueryable<T> query,
            TOpts options,
            Func<IQueryable<T>, TOpts, IQueryable<T>>? extraFilters = null)
        {
            var props = _propsCache.GetOrAdd(typeof(TOpts), t => t.GetProperties());
            var parameter = Expression.Parameter(typeof(T), "x");
            Expression? predicate = null;

            foreach (var prop in props)
            {
                var value = prop.GetValue(options);
                if (value is null) continue;

                var targetProp = typeof(T).GetProperty(prop.Name);
                if (targetProp is null) continue;

                var left  = Expression.Property(parameter, targetProp);
                Expression right;

                if (targetProp.PropertyType == prop.PropertyType)
                {
                    right = Expression.Constant(value, prop.PropertyType);
                }
                else if (targetProp.PropertyType.IsAssignableFrom(prop.PropertyType))
                {
                    right = Expression.Convert(
                        Expression.Constant(value, prop.PropertyType),
                        targetProp.PropertyType);
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
                query = query.Where(Expression.Lambda<Func<T, bool>>(predicate, parameter));

            if (extraFilters is not null)
                query = extraFilters(query, options);

            return query;
        }

        protected IQueryable<T> ApplySearchOptions<T, TOpts>(
            IQueryable<T> query,
            Action<TOpts> configure,
            Func<IQueryable<T>, TOpts, IQueryable<T>>? extraFilters = null)
            where TOpts : class, new()
        {
            var options = new TOpts();
            configure(options);
            return ApplySearchOptions(query, options, extraFilters);
        }

        protected CursorPage<TEntity> ApplyCursor(
            IQueryable<TEntity> query,
            CursorOptions cursor)
        {
            var decoded = CursorEncoder.Decode(cursor.After);

            if (decoded is not null)
                query = query.Where(x =>
                    x.CreatedAt > decoded.CreatedAt ||
                    (x.CreatedAt == decoded.CreatedAt &&
                     EF.Property<Guid>(x, "Id") > decoded.Id));

            var items = query
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => EF.Property<Guid>(x, "Id"))
                .Take(cursor.Limit + 1)
                .ToList();

            var hasMore = items.Count > cursor.Limit;
            if (hasMore) items.RemoveAt(items.Count - 1);

            var nextCursor = hasMore
                ? CursorEncoder.Encode(items.Last().Id, items.Last().CreatedAt)
                : null;

            return new CursorPage<TEntity> { Data = items, NextCursor = nextCursor };
        }
    }
}
