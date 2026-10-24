// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// A constant list literal, built once when parsed and shared by every
    /// evaluation. It reads like any list; the one in-place writer, Set,
    /// copies it first (<see cref="Frozen.Thaw"/>).
    /// </summary>
    internal sealed class FrozenList(IEnumerable<object?> items) : List<object?>(items);

    /// <summary>A constant object literal: see <see cref="FrozenList"/>.</summary>
    internal sealed class FrozenObject(IEnumerable<KeyValuePair<string, object?>> members)
        : Dictionary<string, object?>(members);

    internal static class Frozen
    {
        /// <summary>A shared constant as a private copy (its insides stay shared until written); anything else as is.</summary>
        /// <summary>A deep, shared-safe copy: lists and objects become frozen ones; anything else as is.</summary>
        public static object? Freeze(object? value) => Values.Unwrap(value) switch
        {
            FrozenList or FrozenObject or JsonView => value,
            IDictionary<string, object?> dict => new FrozenObject(dict.Select(p => KeyValuePair.Create(p.Key, Freeze(p.Value)))),
            System.Collections.IList list => new FrozenList(list.Cast<object?>().Select(Freeze)),
            var other => other
        };

        public static object? Thaw(object? value) => value switch
        {
            FrozenList list => new List<object?>(list),
            FrozenObject dict => new Dictionary<string, object?>(dict),
            JsonArrayView list => new List<object?>(list),
            JsonObjectView dict => new Dictionary<string, object?>(dict),
            _ => value
        };
    }

    /// <summary>
    /// Per-thread pool of argument arrays, used as a stack: a call rents on
    /// entry and returns on exit, so nested calls (a lambda calling functions
    /// while an outer function still reads its arguments) each get their own.
    /// </summary>
    internal static class ArgumentArrays
    {
        private const int MaxLength = 8;
        private const int MaxKept = 32;

        [ThreadStatic]
        private static Stack<object?[]>[]? _free;

        public static object?[] Rent(int length)
        {
            if (length == 0)
                return [];
            if (length > MaxLength)
                return new object?[length];
            return Free(length).TryPop(out var array) ? array : new object?[length];
        }

        public static void Return(object?[] array)
        {
            if (array.Length is 0 or > MaxLength)
                return;
            Array.Clear(array);
            var free = Free(array.Length);
            if (free.Count < MaxKept)
                free.Push(array);
        }

        // This thread's stack of free arrays of a length, made on first use.
        private static Stack<object?[]> Free(int length)
            => (_free ??= new Stack<object?[]>[MaxLength + 1])[length] ??= new Stack<object?[]>();
    }
}
