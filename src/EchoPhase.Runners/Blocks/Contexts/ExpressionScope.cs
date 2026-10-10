// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace EchoPhase.Runners.Blocks.Contexts
{
    /// <summary>
    /// What an expression in a block sees: the run's variables, plus
    /// <c>input</c> (the output of the block that triggered this one) and
    /// <c>blocks</c> (every block's latest output, as <c>blocks.b3</c>).
    /// Those two shadow variables of the same name; writes go to the variables.
    /// </summary>
    public sealed class ExpressionScope : IDictionary<string, object>
    {
        public const string InputKey = "input";
        public const string BlocksKey = "blocks";

        private readonly IDictionary<string, object> _variables;
        private readonly object? _input;
        private readonly object _blocks;

        public ExpressionScope(IDictionary<string, object> variables, object? input, IDictionary<string, object?> blocks)
        {
            _variables = variables;
            _input = input;
            _blocks = blocks;
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
        {
            switch (key)
            {
                case InputKey when _input is not null:
                    value = _input;
                    return true;
                case BlocksKey:
                    value = _blocks;
                    return true;
                default:
                    return _variables.TryGetValue(key, out value);
            }
        }

        public object this[string key]
        {
            get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);
            set => _variables[key] = value;
        }

        public bool ContainsKey(string key) => TryGetValue(key, out _);

        public ICollection<string> Keys => [.. _variables.Keys, InputKey, BlocksKey];

        public ICollection<object> Values => [.. Keys.Select(k => this[k])];

        public int Count => Keys.Count;

        public bool IsReadOnly => false;

        public void Add(string key, object value) => _variables.Add(key, value);

        public bool Remove(string key) => _variables.Remove(key);

        public void Add(KeyValuePair<string, object> item) => _variables.Add(item);

        public void Clear() => _variables.Clear();

        public bool Contains(KeyValuePair<string, object> item) => TryGetValue(item.Key, out var v) && Equals(v, item.Value);

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            foreach (var pair in this)
                array[arrayIndex++] = pair;
        }

        public bool Remove(KeyValuePair<string, object> item) => _variables.Remove(item);

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            => Keys.Where(ContainsKey).Select(k => new KeyValuePair<string, object>(k, this[k])).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
