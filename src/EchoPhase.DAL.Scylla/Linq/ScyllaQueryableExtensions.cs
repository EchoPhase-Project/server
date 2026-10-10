// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using System.Reflection;
using EchoPhase.DAL.Scylla.Database;

namespace EchoPhase.DAL.Scylla.Linq
{
    /// <summary>
    /// Async terminal operators and Scylla-specific query options. Async
    /// operators translate the whole query to CQL first, so filtering, ordering
    /// and limits run on the server, as with the sync LINQ operators.
    /// </summary>
    public static class ScyllaQueryableExtensions
    {
        internal static readonly MethodInfo AllowFilteringMethod =
            typeof(ScyllaQueryableExtensions).GetMethod(nameof(AllowFiltering))!;

        /// <summary>
        /// Lets the query use <c>ALLOW FILTERING</c>, i.e. scan rows that the key
        /// does not narrow down. Without it such queries are rejected, so a full
        /// scan is always a deliberate choice.
        /// </summary>
        public static IQueryable<T> AllowFiltering<T>(this IQueryable<T> source)
        {
            if (source.Provider is not QueryProvider)
                return source;

            return source.Provider.CreateQuery<T>(
                Expression.Call(null, AllowFilteringMethod.MakeGenericMethod(typeof(T)), source.Expression));
        }

        public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
            => Provider(source).ExecuteSequenceAsync<T>(source.Expression, ct);

        public static async Task<T[]> ToArrayAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
            => (await source.ToListAsync(ct)).ToArray();

        public static async Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
            => (await source.Take(1).ToListAsync(ct)).FirstOrDefault();

        public static Task<T?> FirstOrDefaultAsync<T>(
            this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => source.Where(predicate).FirstOrDefaultAsync(ct);

        public static async Task<T> FirstAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
        {
            var items = await source.Take(1).ToListAsync(ct);
            return items.Count > 0 ? items[0] : throw new InvalidOperationException("Sequence contains no elements");
        }

        public static Task<T> FirstAsync<T>(
            this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => source.Where(predicate).FirstAsync(ct);

        public static async Task<T?> SingleOrDefaultAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
        {
            // Two rows are enough to tell "one" from "more than one".
            var items = await source.Take(2).ToListAsync(ct);
            return items.Count switch
            {
                0 => default,
                1 => items[0],
                _ => throw new InvalidOperationException("Sequence contains more than one element")
            };
        }

        public static Task<T?> SingleOrDefaultAsync<T>(
            this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => source.Where(predicate).SingleOrDefaultAsync(ct);

        public static async Task<T> SingleAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
        {
            var items = await source.Take(2).ToListAsync(ct);
            return items.Count switch
            {
                1 => items[0],
                0 => throw new InvalidOperationException("Sequence contains no elements"),
                _ => throw new InvalidOperationException("Sequence contains more than one element")
            };
        }

        public static Task<T> SingleAsync<T>(
            this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => source.Where(predicate).SingleAsync(ct);

        public static async Task<bool> AnyAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
            => (await source.Take(1).ToListAsync(ct)).Count > 0;

        public static Task<bool> AnyAsync<T>(
            this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => source.Where(predicate).AnyAsync(ct);

        public static Task<long> LongCountAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
            => Provider(source).CountAsync<T>(source.Expression, ct);

        public static async Task<int> CountAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
            => checked((int)await source.LongCountAsync(ct));

        public static Task<int> CountAsync<T>(
            this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => source.Where(predicate).CountAsync(ct);

        /// <summary>
        /// One page of up to <paramref name="pageSize"/> rows. Pass the returned
        /// <see cref="ScyllaPage{T}.NextPagingState"/> to get the next page; memory
        /// stays bounded however many rows match. Do not combine with Take().
        /// </summary>
        public static Task<ScyllaPage<T>> ToPageAsync<T>(
            this IQueryable<T> source, int pageSize, byte[]? pagingState = null, CancellationToken ct = default)
            => Provider(source).ExecutePageAsync<T>(source.Expression, pageSize, pagingState, ct);

        private static QueryProvider Provider<T>(IQueryable<T> source)
            => source.Provider as QueryProvider
               ?? throw new InvalidOperationException(
                   "Async Scylla operators need a query that starts from a Scylla DbSet.");
    }
}
