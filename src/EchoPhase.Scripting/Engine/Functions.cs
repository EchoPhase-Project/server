// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// Built-in functions. Names are case-insensitive; argument counts are
    /// checked when the expression is parsed, not when it runs.
    /// </summary>
    internal static class Functions
    {
        private sealed record Definition(int MinArgs, int MaxArgs, Func<object?[], object?> Body);

        private const int Many = int.MaxValue;

        private static readonly Dictionary<string, Definition> All = new(StringComparer.OrdinalIgnoreCase)
        {
            ["min"] = new(1, Many, a => a.Select(Values.ToNumber).Min()),
            ["max"] = new(1, Many, a => a.Select(Values.ToNumber).Max()),
            ["abs"] = new(1, 1, a => Math.Abs(Values.ToNumber(a[0]))),
            ["sqrt"] = new(1, 1, a => Math.Sqrt(Values.ToNumber(a[0]))),
            ["floor"] = new(1, 1, a => Math.Floor(Values.ToNumber(a[0]))),
            ["ceil"] = new(1, 1, a => Math.Ceiling(Values.ToNumber(a[0]))),
            // Half away from zero, as people round (2.5 -> 3), to N digits.
            ["round"] = new(1, 2, a => Math.Round(Values.ToNumber(a[0]),
                a.Length > 1 ? (int)Values.ToNumber(a[1]) : 0, MidpointRounding.AwayFromZero)),
            ["len"] = new(1, 1, a => Values.Unwrap(a[0]) switch
            {
                null => 0d,
                string s => s.Length,
                ICollection c => c.Count,
                var v => throw new InvalidOperationException($"{Values.Describe(v)} has no length.")
            }),
            ["lower"] = new(1, 1, a => Values.Format(a[0]).ToLowerInvariant()),
            ["upper"] = new(1, 1, a => Values.Format(a[0]).ToUpperInvariant()),
            ["trim"] = new(1, 1, a => Values.Format(a[0]).Trim()),
            ["str"] = new(1, 1, a => Values.Format(a[0])),
            ["num"] = new(1, 1, a => Values.ToNumber(a[0])),
            // Text in text, or a value in a list (by ==).
            ["contains"] = new(2, 2, a => Values.Unwrap(a[0]) switch
            {
                null => false,
                string s => s.Contains(Values.Format(a[1]), StringComparison.Ordinal),
                IEnumerable e => e.Cast<object?>().Any(item => Values.AreEqual(item, a[1])),
                var v => throw new InvalidOperationException($"{Values.Describe(v)} cannot contain values.")
            }),
            // The first argument that is not null.
            ["coalesce"] = new(1, Many, a => a.FirstOrDefault(v => Values.Unwrap(v) is not null)),
        };

        public static Func<object?[], object?> Resolve(string name, int arguments, int position)
        {
            if (!All.TryGetValue(name, out var definition))
                throw new ScriptSyntaxException($"Unknown function '{name}'", position);

            if (arguments < definition.MinArgs || arguments > definition.MaxArgs)
                throw new ScriptSyntaxException(definition.MinArgs == definition.MaxArgs
                    ? $"'{name}' takes {definition.MinArgs} argument(s), not {arguments}"
                    : definition.MaxArgs == Many
                        ? $"'{name}' takes at least {definition.MinArgs} argument(s)"
                        : $"'{name}' takes {definition.MinArgs}-{definition.MaxArgs} arguments, not {arguments}", position);

            return definition.Body;
        }
    }
}
