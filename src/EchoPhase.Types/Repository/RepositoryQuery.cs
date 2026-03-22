// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using EchoPhase.DAL.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.Types.Repository
{
    /// <summary>
    /// Fluent query builder for EF Core repositories.
    /// </summary>
    public class RepositoryQuery<TEntity>
        where TEntity : class, ITrackingEntity, IIdentifiable
    {
        protected IQueryable<TEntity> _query;
        private CursorOptions? _cursor;

        public RepositoryQuery(IQueryable<TEntity> query)
        {
            _query = query;
        }

        // ── Filters ───────────────────────────────────────────────────────────

        public RepositoryQuery<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
        {
            _query = _query.Where(predicate);
            return this;
        }

        public RepositoryQuery<TEntity> Include<TProperty>(
            Expression<Func<TEntity, TProperty>> path)
        {
            _query = _query.Include(path);
            return this;
        }

        public RepositoryQuery<TEntity> Include(string navigationPropertyPath)
        {
            _query = _query.Include(navigationPropertyPath);
            return this;
        }

        public RepositoryQuery<TEntity> OrderBy<TKey>(
            Expression<Func<TEntity, TKey>> keySelector)
        {
            _query = _query.OrderBy(keySelector);
            return this;
        }

        public RepositoryQuery<TEntity> OrderByDescending<TKey>(
            Expression<Func<TEntity, TKey>> keySelector)
        {
            _query = _query.OrderByDescending(keySelector);
            return this;
        }

        public RepositoryQuery<TEntity> Take(int count)
        {
            _query = _query.Take(count);
            return this;
        }

        public RepositoryQuery<TEntity> Skip(int count)
        {
            _query = _query.Skip(count);
            return this;
        }

        public RepositoryQuery<TEntity> AsNoTracking()
        {
            _query = _query.AsNoTracking();
            return this;
        }


        // ── Cursor (string / opaque) ──────────────────────────────────────────

        /// <summary>
        /// Applies opaque base64 string cursor pagination — backward-compatible path.
        /// </summary>
        public RepositoryQuery<TEntity> WithCursor(CursorOptions cursor)
        {
            var decoded = CursorEncoder.Decode(cursor.After);

            if (decoded is not null)
                _query = _query.Where(x =>
                    x.CreatedAt > decoded.CreatedAt ||
                    (x.CreatedAt == decoded.CreatedAt &&
                     EF.Property<Guid>(x, "Id") > decoded.Id));

            _query = _query
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => EF.Property<Guid>(x, "Id"))
                .Take(cursor.Limit + 1);

            _cursor = cursor;
            return this;
        }

        public RepositoryQuery<TEntity> WithCursor(Action<CursorOptions> configure)
        {
            var cursor = new CursorOptions();
            configure(cursor);
            return WithCursor(cursor);
        }

        // ── Cursor (typed) ────────────────────────────────────────────────────

        /// <summary>
        /// Typed cursor overload.  The caller provides a predicate that filters records
        /// "after" the cursor value — no base64 encoding needed.
        /// </summary>
        /// <example>
        /// <code>
        /// query.WithCursor(
        ///     options,
        ///     after => x => x.Id > after);
        /// </code>
        /// </example>
        public RepositoryQuery<TEntity> WithCursor<TCursor>(
            CursorOptions<TCursor> options,
            Func<TCursor, Expression<Func<TEntity, bool>>> afterPredicate,
            Func<TEntity, TCursor> cursorSelector)
            where TCursor : notnull
        {
            if (options.After is not null)
                _query = _query.Where(afterPredicate(options.After));

            _query = _query
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => EF.Property<Guid>(x, "Id"))
                .Take(options.Limit + 1);

            // Store as opaque cursor by mapping through the provided selector
            _cursor = new CursorOptions { Limit = options.Limit };
            _typedCursorSelector = entity =>
            {
                var val = cursorSelector(entity);
                return val?.ToString() ?? string.Empty;
            };

            return this;
        }

        private Func<TEntity, string>? _typedCursorSelector;

        // ── Terminals ─────────────────────────────────────────────────────────

        public virtual TEntity? FirstOrDefault()
            => _query.FirstOrDefault();

        public virtual TEntity? FirstOrDefault(Expression<Func<TEntity, bool>> predicate)
            => _query.FirstOrDefault(predicate);

        public virtual List<TEntity> ToList()
            => _query.ToList();

        public virtual HashSet<TEntity> ToHashSet()
            => _query.ToHashSet();

        public virtual bool Any()
            => _query.Any();

        public virtual bool Any(Expression<Func<TEntity, bool>> predicate)
            => _query.Any(predicate);

        public virtual int Count()
            => _query.Count();

        /// <summary>
        /// Materialises the query into a <see cref="CursorPage{T}"/>.
        /// Works with both the opaque-string cursor and the typed cursor overloads.
        /// </summary>
        public virtual CursorPage<TEntity> ToPage()
        {
            var items = _query.ToList();

            if (_cursor is null)
                return new CursorPage<TEntity> { Data = items };

            var hasMore = items.Count > _cursor.Limit;
            if (hasMore) items.RemoveAt(items.Count - 1);

            string? nextCursor = null;
            if (hasMore)
            {
                var last = items.Last();
                nextCursor = _typedCursorSelector is not null
                    ? _typedCursorSelector(last)
                    : CursorEncoder.Encode(last.Id, last.CreatedAt);
            }

            return new CursorPage<TEntity> { Data = items, NextCursor = nextCursor };
        }
    }
}
