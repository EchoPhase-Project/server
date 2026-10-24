// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Scripting;

namespace EchoPhase.Runners.Blocks.Contexts
{
    /// <summary>
    /// What an expression in a block sees: the run's variables, plus
    /// <c>input</c> (the output of the block that triggered this one),
    /// <c>blocks</c> (every block's latest output, as <c>blocks.b3</c>) and
    /// <c>errors</c> (the run's block failures so far, handled ones included,
    /// as <c>errors[-1].message</c>). Those shadow variables of the same name;
    /// writes go to the variables. The date functions also find the
    /// program's time zone here, under a name programs cannot write.
    /// </summary>
    public sealed class ExpressionScope : IDictionary<string, object>
    {
        public const string InputKey = "input";
        public const string BlocksKey = "blocks";
        public const string ErrorsKey = "errors";

        private readonly IDictionary<string, object> _variables;
        private readonly object? _input;
        private readonly object _blocks;
        private readonly IList<BlockError> _errors;
        private readonly string? _timeZone;

        // errors as the language sees it, rebuilt only when an error was added:
        // reading it in a loop must not copy the list every time.
        private object _errorsValue = Array.Empty<object?>();
        private int _errorsSeen = -1;

        public ExpressionScope(
            IDictionary<string, object> variables, object? input, IDictionary<string, object?> blocks,
            IList<BlockError>? errors = null, string? timeZone = null)
        {
            _variables = variables;
            _input = input;
            _blocks = blocks;
            _errors = errors ?? [];
            _timeZone = timeZone;
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
                case ErrorsKey:
                    if (_errorsSeen != _errors.Count)
                    {
                        // Frozen: a Set into it copies instead of changing what later reads see.
                        var errors = _errors.Select(e => (object?)e.ToValue()).ToList();
                        _errorsValue = Eval.Freeze(errors) ?? errors;
                        _errorsSeen = _errors.Count;
                    }
                    value = _errorsValue;
                    return true;
                case Eval.TimeZoneVariable when _timeZone is not null:
                    value = _timeZone;
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

        public ICollection<string> Keys => [.. _variables.Keys.Except([InputKey, BlocksKey, ErrorsKey]), InputKey, BlocksKey, ErrorsKey];

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
