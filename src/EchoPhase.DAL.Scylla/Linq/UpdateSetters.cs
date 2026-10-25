// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla.Linq
{
    /// <summary>
    /// What an ExecuteUpdate writes, computed by Scylla from the stored value:
    /// counters, collection appends and removals, map entries, plain sets.
    /// Nothing is read first, so concurrent updates lose nothing.
    /// </summary>
    public sealed class UpdateSetters<TEntity>
    {
        private readonly IEntityBuilder _builder;
        private readonly List<(string Clause, object?[] Values, bool Idempotent)> _assignments = new();

        internal UpdateSetters(IEntityBuilder builder)
        {
            _builder = builder;
        }

        internal IReadOnlyList<(string Clause, object?[] Values, bool Idempotent)> Assignments => _assignments;

        /// <summary>column = value.</summary>
        public UpdateSetters<TEntity> Set<TProp>(Expression<Func<TEntity, TProp>> property, TProp value)
        {
            var (name, column) = Column(property);
            return Add($"{column} = ?", [_builder.ToProvider(name, value)], idempotent: true);
        }

        /// <summary>column = null: deletes the stored value.</summary>
        public UpdateSetters<TEntity> Clear<TProp>(Expression<Func<TEntity, TProp>> property)
            => Add($"{Column(property).Column} = ?", [null], idempotent: true);

        /// <summary>A counter column: column = column + delta (negative to decrease).</summary>
        public UpdateSetters<TEntity> Increment(Expression<Func<TEntity, long>> counter, long delta = 1)
            => Add($"{Column(counter).Column} = {Column(counter).Column} + ?", [delta], idempotent: false);

        /// <summary>A list or set column: adds the items (at the end of a list).</summary>
        public UpdateSetters<TEntity> Append<TItem>(Expression<Func<TEntity, IEnumerable<TItem>?>> collection, IEnumerable<TItem> items)
        {
            var (_, column) = Column(collection);
            var isSet = IsSet(collection.Body);
            return Add($"{column} = {column} + ?", [Typed(items, isSet)], idempotent: isSet);
        }

        /// <summary>A list column: adds the items at its start.</summary>
        public UpdateSetters<TEntity> Prepend<TItem>(Expression<Func<TEntity, IEnumerable<TItem>?>> list, IEnumerable<TItem> items)
        {
            var (_, column) = Column(list);
            return Add($"{column} = ? + {column}", [items.ToList()], idempotent: false);
        }

        /// <summary>A list or set column: removes every occurrence of the items.</summary>
        public UpdateSetters<TEntity> Remove<TItem>(Expression<Func<TEntity, IEnumerable<TItem>?>> collection, IEnumerable<TItem> items)
        {
            var (_, column) = Column(collection);
            return Add($"{column} = {column} - ?", [Typed(items, IsSet(collection.Body))], idempotent: true);
        }

        /// <summary>A map column: column[key] = value.</summary>
        public UpdateSetters<TEntity> Put<TKey, TValue>(Expression<Func<TEntity, IDictionary<TKey, TValue>?>> map, TKey key, TValue value)
            => Add($"{Column(map).Column}[?] = ?", [key, value], idempotent: true);

        /// <summary>A map column: removes the entries of these keys.</summary>
        public UpdateSetters<TEntity> RemoveKeys<TKey, TValue>(Expression<Func<TEntity, IDictionary<TKey, TValue>?>> map, IEnumerable<TKey> keys)
        {
            var (_, column) = Column(map);
            return Add($"{column} = {column} - ?", [keys.ToHashSet()], idempotent: true);
        }

        private UpdateSetters<TEntity> Add(string clause, object?[] values, bool idempotent)
        {
            _assignments.Add((clause, values, idempotent));
            return this;
        }

        private (string Name, string Column) Column(LambdaExpression property)
        {
            var body = property.Body;
            while (body is UnaryExpression { NodeType: ExpressionType.Convert } convert)
                body = convert.Operand;
            if (body is not MemberExpression { Expression: ParameterExpression } member)
                throw new NotSupportedException($"Expected a property, e.g. x => x.Views: {property}");
            if (_builder.GetPrimaryKey().Contains(member.Member.Name))
                throw new InvalidOperationException($"{member.Member.Name} is part of the primary key and cannot be updated.");
            return (member.Member.Name, _builder.GetColumn(member.Member.Name));
        }

        private static bool IsSet(Expression body)
        {
            while (body is UnaryExpression { NodeType: ExpressionType.Convert } convert)
                body = convert.Operand;
            var type = body.Type;
            return type.IsGenericType && type.GetGenericTypeDefinition() is var d
                   && (d == typeof(HashSet<>) || d == typeof(ISet<>) || d == typeof(SortedSet<>) || d == typeof(IReadOnlySet<>));
        }

        // The driver serializes by the bound type: a set column wants a set.
        private static object Typed<TItem>(IEnumerable<TItem> items, bool asSet)
            => asSet ? items.ToHashSet() : items.ToList();
    }
}
