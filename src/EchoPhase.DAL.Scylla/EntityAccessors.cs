// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla
{
    /// <summary>A mapped property with a compiled getter: no reflection per read.</summary>
    internal sealed record PropertyAccessor(string Name, Type Type, Func<object, object?> Get, bool IsKey);

    /// <summary>An entity's identity: its type and primary key values, compared by value.</summary>
    internal readonly struct EntityKey : IEquatable<EntityKey>
    {
        private readonly Type _type;
        private readonly object?[] _values;
        private readonly int _hash;

        public EntityKey(Type type, object?[] values)
        {
            _type = type;
            _values = values;
            var hash = new HashCode();
            hash.Add(type);
            foreach (var value in values)
                hash.Add(value);
            _hash = hash.ToHashCode();
        }

        public bool Equals(EntityKey other)
        {
            if (_type != other._type || _values.Length != other._values.Length)
                return false;
            for (var i = 0; i < _values.Length; i++)
                if (!Equals(_values[i], other._values[i]))
                    return false;
            return true;
        }

        public override bool Equals(object? obj) => obj is EntityKey other && Equals(other);

        public override int GetHashCode() => _hash;
    }

    /// <summary>
    /// The mapped properties of an entity type, keys first (in primary key order), each with a compiled
    /// getter; built once per type and model. Tracking (snapshots, change
    /// detection, identity) reads entities through these.
    /// </summary>
    internal static class EntityAccessors
    {
        private static readonly ConcurrentDictionary<(Type, IEntityBuilder), PropertyAccessor[]> Cache = new();

        public static PropertyAccessor[] For(Type type, IEntityBuilder builder)
            => Cache.GetOrAdd((type, builder), key => Build(key.Item1, key.Item2));

        private static PropertyAccessor[] Build(Type type, IEntityBuilder builder)
        {
            var keys = builder.GetPrimaryKey();
            return keys.Concat(builder.GetMappedProperties()).Distinct()
                .Select(name => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance))
                .OfType<PropertyInfo>()
                .Where(p => p.CanRead)
                .Select(p =>
                {
                    var target = Expression.Parameter(typeof(object), "entity");
                    var get = Expression.Lambda<Func<object, object?>>(
                        Expression.Convert(Expression.Property(Expression.Convert(target, type), p), typeof(object)),
                        target).Compile();
                    return new PropertyAccessor(p.Name, p.PropertyType, get, keys.Contains(p.Name));
                })
                .ToArray();
        }
    }
}
