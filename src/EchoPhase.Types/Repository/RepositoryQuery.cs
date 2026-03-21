// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using EchoPhase.DAL.Abstractions;
using Microsoft.EntityFrameworkCore;

// ── RepositoryQuery<TEntity> ──────────────────────────────────────────────────

namespace EchoPhase.Types.Repository
{
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

        // ── Cursor ────────────────────────────────────────────────────────────

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

        public virtual CursorPage<TEntity> ToPage()
        {
            var items = _query.ToList();

            if (_cursor is null)
                return new CursorPage<TEntity> { Data = items };

            var hasMore = items.Count > _cursor.Limit;
            if (hasMore) items.RemoveAt(items.Count - 1);

            var nextCursor = hasMore
                ? CursorEncoder.Encode(items.Last().Id, items.Last().CreatedAt)
                : null;

            return new CursorPage<TEntity> { Data = items, NextCursor = nextCursor };
        }
    }
}
