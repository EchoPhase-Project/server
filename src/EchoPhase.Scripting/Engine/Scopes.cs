// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// Names bound by a lambda call or a comprehension, over the variables
    /// around them; read-only. One object per call: the names are the
    /// lambda's own array, so binding one or two of them allocates nothing else.
    /// </summary>
    internal abstract class BoundScope(IDictionary<string, object> outer) : IDictionary<string, object>
    {
        /// <summary>Settable: a reused scope is pointed at the current enclosing one.</summary>
        public IDictionary<string, object> Outer = outer;

        internal abstract bool TryGetBound(string key, out object? value);

        protected abstract IEnumerable<string> BoundNames { get; }

        /// <summary>
        /// A name as the language reads it: bound here (null included: a lambda
        /// parameter may be null), else in the scopes around. False only for a
        /// name bound nowhere.
        /// </summary>
        public static bool Find(IDictionary<string, object> variables, string key, out object? value)
        {
            var scope = variables;
            while (scope is BoundScope bound)
            {
                if (bound.TryGetBound(key, out value))
                    return true;
                scope = bound.Outer;
            }

            if (scope.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>
        /// The dictionary view, for code outside the engine: its values are not
        /// null, so a name bound to null reads as absent here (it still hides an
        /// outer one). The engine reads names with <see cref="Find"/>.
        /// </summary>
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
        {
            if (TryGetBound(key, out var bound))
            {
                value = bound;
                return value is not null;
            }
            return Outer.TryGetValue(key, out value);
        }

        public object this[string key]
        {
            get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);
            set => throw ReadOnly();
        }

        public bool ContainsKey(string key) => TryGetBound(key, out _) || Outer.ContainsKey(key);

        public ICollection<string> Keys => [.. BoundNames.Union(Outer.Keys)];

        public ICollection<object> Values => [.. Keys.Select(k => this[k])];

        public int Count => Keys.Count;

        public bool IsReadOnly => true;

        public void Add(string key, object value) => throw ReadOnly();

        public void Add(KeyValuePair<string, object> item) => throw ReadOnly();

        public bool Remove(string key) => throw ReadOnly();

        public bool Remove(KeyValuePair<string, object> item) => throw ReadOnly();

        public void Clear() => throw ReadOnly();

        public bool Contains(KeyValuePair<string, object> item) => TryGetValue(item.Key, out var v) && Equals(v, item.Value);

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            foreach (var pair in this)
                array[arrayIndex++] = pair;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            => Keys.Select(k => new KeyValuePair<string, object>(k, this[k])).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static NotSupportedException ReadOnly() => new("Lambda parameters cannot be assigned.");
    }

    // Values are fields, not constructor captures, so a scope can be reused:
    // a lambda without lambdas inside rebinds one scope on every call.
    internal sealed class Scope1(string name, object? value, IDictionary<string, object> outer) : BoundScope(outer)
    {
        public object? Value = value;

        internal override bool TryGetBound(string key, out object? bound)
        {
            bound = Value;
            return string.Equals(key, name, StringComparison.Ordinal);
        }

        protected override IEnumerable<string> BoundNames => [name];
    }

    internal sealed class Scope2(string name1, object? value1, string name2, object? value2, IDictionary<string, object> outer)
        : BoundScope(outer)
    {
        public object? Value1 = value1;
        public object? Value2 = value2;

        internal override bool TryGetBound(string key, out object? bound)
        {
            if (string.Equals(key, name1, StringComparison.Ordinal))
            {
                bound = Value1;
                return true;
            }
            bound = Value2;
            return string.Equals(key, name2, StringComparison.Ordinal);
        }

        protected override IEnumerable<string> BoundNames => [name1, name2];
    }

    internal sealed class ScopeN(string[] names, object?[] values, IDictionary<string, object> outer) : BoundScope(outer)
    {
        internal override bool TryGetBound(string key, out object? bound)
        {
            int i = Array.IndexOf(names, key);
            bound = i >= 0 ? values[i] : null;
            return i >= 0;
        }

        protected override IEnumerable<string> BoundNames => names;
    }

    /// <summary>
    /// Shared boxes: an index passed to a lambda costs no allocation below
    /// 4096, a true or false none at all.
    /// </summary>
    internal static class Boxes
    {
        public static readonly object True = true;
        public static readonly object False = false;

        public static object Bool(bool value) => value ? True : False;

        private static readonly object[] Small = [.. Enumerable.Range(0, 4096).Select(i => (object)(double)i)];

        public static object Index(int i) => (uint)i < (uint)Small.Length ? Small[i] : (double)i;
    }
}
