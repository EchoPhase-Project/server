// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// Built-in functions. Names are case-insensitive; argument counts are
    /// checked when the expression is parsed, not when it runs. More in the
    /// other parts: lists (with lambdas), strings and JSON, dates.
    /// </summary>
    internal static partial class Functions
    {
        /// <summary>A body gets the evaluated arguments and, when it needs them (time zone), the variables.</summary>
        /// <summary>
        /// A body that reads the variables (the program's time zone) is not
        /// pure: its value depends on more than its arguments.
        /// </summary>
        private sealed record Definition(int MinArgs, int MaxArgs, Func<object?[], IDictionary<string, object>, object?> Body, bool ReadsVariables = true)
        {
            public Definition(int minArgs, int maxArgs, Func<object?[], object?> body)
                : this(minArgs, maxArgs, (a, _) => body(a), ReadsVariables: false)
            {
            }
        }

        /// <summary>Different on every call: never computed ahead of time.</summary>
        private static readonly HashSet<string> Impure = new(StringComparer.OrdinalIgnoreCase)
        {
            "rand", "randInt", "choice", "shuffle", "randomBytes", "uuid4", "uuid7", "now", "today",
        };

        private const int Many = int.MaxValue;

        private static readonly Dictionary<string, Definition> All;

        // After every part's field initializer, whatever file it is in.
        static Functions()
        {
            All = Build();
        }

        private static Dictionary<string, Definition> Build()
        {
            var all = new Dictionary<string, Definition>(Core, StringComparer.OrdinalIgnoreCase);
            foreach (var part in new[] { Lists, Strings, Dates, Hashing, Randomness, Statistics, Search, Linkage })
                foreach (var (name, definition) in part)
                    all.Add(name, definition);
            return all;
        }

        private static readonly Dictionary<string, Definition> Core = new(StringComparer.OrdinalIgnoreCase)
        {
            // min(1, 2, 3) or min(list)
            ["min"] = new(1, Many, a => Numbers(a).Min()),
            ["max"] = new(1, Many, a => Numbers(a).Max()),
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
            // Text in text, a value in a list (by ==), a key in an object: like "b in a".
            ["contains"] = new(2, 2, a => Values.Contains(a[0], a[1])),
            // The first argument that is not null.
            ["coalesce"] = new(1, Many, a => a.FirstOrDefault(v => Values.Unwrap(v) is not null)),
        };

        /// <summary>The function, and whether the same arguments always give the same value (so a call on constants can be computed when parsed).</summary>
        public static (Func<object?[], IDictionary<string, object>, object?> Body, bool Pure) Resolve(string name, int arguments, int position)
        {
            if (!All.TryGetValue(name, out var definition))
                throw new ScriptSyntaxException($"Unknown function '{name}'", position);

            if (arguments < definition.MinArgs || arguments > definition.MaxArgs)
                throw new ScriptSyntaxException(definition.MinArgs == definition.MaxArgs
                    ? $"'{name}' takes {definition.MinArgs} argument(s), not {arguments}"
                    : definition.MaxArgs == Many
                        ? $"'{name}' takes at least {definition.MinArgs} argument(s)"
                        : $"'{name}' takes {definition.MinArgs}-{definition.MaxArgs} arguments, not {arguments}", position);

            return (definition.Body, !definition.ReadsVariables && !Impure.Contains(name));
        }

        private static IEnumerable<double> Numbers(object?[] arguments)
            => (arguments is [var only] && Values.Unwrap(only) is IList list ? list.Cast<object?>() : arguments)
                .Select(Values.ToNumber)
                .DefaultIfEmpty(double.NaN);
    }
}
