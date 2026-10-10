// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Abstractions.Repository
{
    /// <summary>
    /// Fluent query builder for EF Core repositories.
    /// </summary>
    public class RepositoryQuery<TEntity>
        where TEntity : class, ITrackingEntity, IIdentifiable
    {
        protected IQueryable<TEntity> Query;
        private CursorOptions? _cursor;

        public RepositoryQuery(IQueryable<TEntity> query)
        {
            Query = query;
        }

        // ── Filters ───────────────────────────────────────────────────────────

        public RepositoryQuery<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
        {
            Query = Query.Where(predicate);
            return this;
        }

        public RepositoryQuery<TEntity> Include<TProperty>(
            Expression<Func<TEntity, TProperty>> path)
        {
            Query = Query.Include(path);
            return this;
        }

        public RepositoryQuery<TEntity> Include(string navigationPropertyPath)
        {
            Query = Query.Include(navigationPropertyPath);
            return this;
        }

        public RepositoryQuery<TEntity> OrderBy<TKey>(
            Expression<Func<TEntity, TKey>> keySelector)
        {
            Query = Query.OrderBy(keySelector);
            return this;
        }

        public RepositoryQuery<TEntity> OrderByDescending<TKey>(
            Expression<Func<TEntity, TKey>> keySelector)
        {
            Query = Query.OrderByDescending(keySelector);
            return this;
        }

        public RepositoryQuery<TEntity> Take(int count)
        {
            Query = Query.Take(count);
            return this;
        }

        public RepositoryQuery<TEntity> Skip(int count)
        {
            Query = Query.Skip(count);
            return this;
        }

        public RepositoryQuery<TEntity> AsNoTracking()
        {
            Query = Query.AsNoTracking();
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
                Query = Query.Where(x =>
                    x.CreatedAt > decoded.CreatedAt ||
                    (x.CreatedAt == decoded.CreatedAt &&
                     EF.Property<Guid>(x, "Id") > decoded.Id));

            Query = Query
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
                Query = Query.Where(afterPredicate(options.After));

            Query = Query
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
            => Query.FirstOrDefault();

        public virtual TEntity? FirstOrDefault(Expression<Func<TEntity, bool>> predicate)
            => Query.FirstOrDefault(predicate);

        public virtual List<TEntity> ToList()
            => Query.ToList();

        public virtual HashSet<TEntity> ToHashSet()
            => Query.ToHashSet();

        public virtual bool Any()
            => Query.Any();

        public virtual bool Any(Expression<Func<TEntity, bool>> predicate)
            => Query.Any(predicate);

        public virtual int Count()
            => Query.Count();

        /// <summary>The SQL EF would run; for logging and tests.</summary>
        public virtual string ToQueryString() => Query.ToQueryString();

        // ── Async terminals ───────────────────────────────────────────────────

        public virtual Task<TEntity?> FirstOrDefaultAsync(CancellationToken ct = default)
            => Query.FirstOrDefaultAsync(ct);

        public virtual Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
            => Query.FirstOrDefaultAsync(predicate, ct);

        public virtual Task<List<TEntity>> ToListAsync(CancellationToken ct = default)
            => Query.ToListAsync(ct);

        public virtual Task<HashSet<TEntity>> ToHashSetAsync(CancellationToken ct = default)
            => Query.ToHashSetAsync(ct);

        public virtual Task<bool> AnyAsync(CancellationToken ct = default)
            => Query.AnyAsync(ct);

        public virtual Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
            => Query.AnyAsync(predicate, ct);

        public virtual Task<int> CountAsync(CancellationToken ct = default)
            => Query.CountAsync(ct);

        /// <inheritdoc cref="ToPage"/>
        public virtual async Task<CursorPage<TEntity>> ToPageAsync(CancellationToken ct = default)
            => BuildPage(await ToListAsync(ct));

        /// <summary>
        /// Materialises the query into a <see cref="CursorPage{T}"/>.
        /// Works with both the opaque-string cursor and the typed cursor overloads.
        /// </summary>
        public virtual CursorPage<TEntity> ToPage()
            => BuildPage(Query.ToList());

        private CursorPage<TEntity> BuildPage(List<TEntity> items)
        {
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
