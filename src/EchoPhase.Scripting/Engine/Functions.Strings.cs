// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EchoPhase.Scripting.Engine
{
    // Text and JSON. Arguments are read as text (str), so numbers work too.
    internal static partial class Functions
    {
        public const int MaxPad = 10_000;

        // Patterns come from programs: a catastrophic one stops here, not the worker.
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

        private static readonly Dictionary<string, Definition> Strings = new(StringComparer.OrdinalIgnoreCase)
        {
            // split('a,b', ',') -> ['a', 'b']; split('ab', '') -> ['a', 'b']
            ["split"] = new(2, 2, a => Split(Text(a[0]), Text(a[1]))),
            ["join"] = new(1, 2, a =>
            {
                var xs = ListOf(a, "join");
                var separator = a.Length > 1 ? Text(a[1]) : ",";
                var text = new System.Text.StringBuilder();
                for (int i = 0; i < xs.Count; i++)
                {
                    Budget.Spend(1);
                    if (i > 0)
                        text.Append(separator);
                    text.Append(Values.Format(xs[i]));
                }
                return text.ToString();
            }),
            // Every occurrence, case-sensitive.
            ["replace"] = new(3, 3, a => Text(a[1]) is { Length: > 0 } find ? Text(a[0]).Replace(find, Text(a[2]), StringComparison.Ordinal) : Text(a[0])),
            ["startsWith"] = new(2, 2, a => Text(a[0]).StartsWith(Text(a[1]), StringComparison.Ordinal)),
            ["endsWith"] = new(2, 2, a => Text(a[0]).EndsWith(Text(a[1]), StringComparison.Ordinal)),
            // substring(s, start, length?); a negative start counts from the end; out of range is clamped.
            ["substring"] = new(2, 3, a => Substring(Text(a[0]), Values.ToNumber(a[1]), a.Length > 2 ? Values.ToNumber(a[2]) : null)),
            // In text or in a list (by ==); -1 when absent.
            ["indexOf"] = new(2, 2, a => (double)(Values.Unwrap(a[0]) switch
            {
                IList list => list.Cast<object?>().Select((x, i) => (x, i)).FirstOrDefault(p => Values.AreEqual(p.x, a[1]), (null, -1)).Item2,
                var v => Values.Format(v).IndexOf(Text(a[1]), StringComparison.Ordinal)
            })),
            ["padStart"] = new(2, 3, a => Text(a[0]).PadLeft(Width(a[1]), PadChar(a))),
            ["padEnd"] = new(2, 3, a => Text(a[0]).PadRight(Width(a[1]), PadChar(a))),
            // Python's strip: whitespace, or any of the given characters, from both ends (l: start, r: end).
            ["strip"] = new(1, 2, a => a.Length > 1 ? Text(a[0]).Trim(Text(a[1]).ToCharArray()) : Text(a[0]).Trim()),
            ["lstrip"] = new(1, 2, a => a.Length > 1 ? Text(a[0]).TrimStart(Text(a[1]).ToCharArray()) : Text(a[0]).TrimStart()),
            ["rstrip"] = new(1, 2, a => a.Length > 1 ? Text(a[0]).TrimEnd(Text(a[1]).ToCharArray()) : Text(a[0]).TrimEnd()),
            ["capitalize"] = new(1, 1, a => Text(a[0]) is { Length: > 0 } s ? char.ToUpperInvariant(s[0]) + s[1..] : string.Empty),
            // .NET regular expressions, at most 100 ms each.
            ["matches"] = new(2, 2, a => Regex.IsMatch(Text(a[0]), Text(a[1]), RegexOptions.None, RegexTimeout)),
            ["regexReplace"] = new(3, 3, a => Regex.Replace(Text(a[0]), Text(a[1]), Text(a[2]), RegexOptions.None, RegexTimeout)),
            // The first match's groups: [whole, group1, ...], or null.
            ["regexMatch"] = new(2, 2, a => Regex.Match(Text(a[0]), Text(a[1]), RegexOptions.None, RegexTimeout) is { Success: true } m
                ? m.Groups.Cast<Group>().Select(g => (object?)g.Value).ToList()
                : null),
            // Any value as JSON text; parseJson reads it back.
            ["json"] = new(1, 1, a => Values.ToJson(a[0])),
            ["parseJson"] = new(1, 1, a =>
            {
                try
                {
                    using var document = JsonDocument.Parse(Text(a[0]));
                    return Values.FromJson(document.RootElement);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"'parseJson' got invalid JSON: {ex.Message}");
                }
            }),
        };

        private static string Text(object? value) => Values.Format(value);

        private static List<object?> Split(string text, string separator)
        {
            var parts = separator.Length == 0
                ? text.Select(c => (object?)c.ToString()).ToList()
                : text.Split(separator).Select(p => (object?)p).ToList();
            Budget.Spend(parts.Count);
            return parts;
        }

        private static string Substring(string text, double start, double? length)
        {
            int from = (int)Math.Clamp(start < 0 ? text.Length + start : start, 0, text.Length);
            int count = length is { } l ? (int)Math.Clamp(l, 0, text.Length - from) : text.Length - from;
            return text.Substring(from, count);
        }

        private static int Width(object? value)
        {
            var width = Values.ToNumber(value);
            return width <= MaxPad
                ? (int)Math.Max(0, width)
                : throw new InvalidOperationException($"Padding is limited to {MaxPad} characters.");
        }

        private static char PadChar(object?[] arguments)
            => arguments.Length > 2 && Text(arguments[2]) is { Length: > 0 } pad ? pad[0] : ' ';
    }
}
