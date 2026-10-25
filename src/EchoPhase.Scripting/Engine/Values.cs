// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// The value rules of the language. Values are null, bool, numbers
    /// (double from literals and arithmetic, long or int from variables),
    /// strings, lists, dictionaries and JSON elements (read as their plain
    /// equivalents).
    /// </summary>
    internal static class Values
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        /// <summary>JSON elements as plain values; everything else as is.</summary>
        public static object? Unwrap(object? value) => value is JsonElement json ? FromJson(json) : value;

        public static object? FromJson(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out long l) ? (object)l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => FromJson(p.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(FromJson).ToList(),
            _ => null
        };

        public static bool IsNumber(object? value) => value is double or long or int or float or decimal or short or byte
            or uint or ulong or ushort or sbyte;

        /// <summary>Numbers, bools (1/0) and numeric strings count as numbers.</summary>
        public static bool TryNumber(object? value, out double number)
        {
            switch (Unwrap(value))
            {
                case double d:
                    number = d;
                    return true;
                case bool b:
                    number = b ? 1 : 0;
                    return true;
                case string s:
                    return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
                case var v when IsNumber(v):
                    number = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                    return true;
                default:
                    number = 0;
                    return false;
            }
        }

        public static double ToNumber(object? value)
            => TryNumber(value, out var number)
                ? number
                : throw new InvalidOperationException($"{Describe(value)} is not a number.");

        /// <summary>
        /// Truthiness: null, false, 0, NaN, "" and empty collections are false;
        /// everything else is true.
        /// </summary>
        public static bool ToBool(object? value) => Unwrap(value) switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            double d => d != 0 && !double.IsNaN(d),
            ICollection c => c.Count > 0,
            var v when IsNumber(v) => Convert.ToDouble(v, CultureInfo.InvariantCulture) != 0,
            _ => true
        };

        /// <summary>
        /// Text for templates and string concatenation, culture-independent:
        /// whole numbers without ".0", bools as true/false, collections as JSON.
        /// </summary>
        public static string Format(object? value) => Unwrap(value) switch
        {
            null => string.Empty,
            string s => s,
            bool b => b ? "true" : "false",
            double d when d == Math.Floor(d) && Math.Abs(d) < 1e15 => ((long)d).ToString(CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture),
            // As its runtime type: a dictionary is an object, not a list of pairs.
            IEnumerable e => JsonSerializer.Serialize<object>(e, Json),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            var v => v.ToString() ?? string.Empty
        };

        public static string ToJson(object? value) => JsonSerializer.Serialize(Unwrap(value), Json);

        /// <summary>
        /// Equality: null only equals null; numbers (and numeric strings or
        /// bools against numbers) compare by value; strings ordinally; other
        /// values by Equals.
        /// </summary>
        public static bool AreEqual(object? left, object? right)
        {
            left = Unwrap(left);
            right = Unwrap(right);

            if (left is null || right is null)
                return left is null && right is null;
            if (left is string ls && right is string rs)
                return string.Equals(ls, rs, StringComparison.Ordinal);
            if (left is bool lb && right is bool rb)
                return lb == rb;
            if ((IsNumber(left) || IsNumber(right)) && TryNumber(left, out var ln) && TryNumber(right, out var rn))
                return ln == rn;
            return left.Equals(right);
        }

        /// <summary>
        /// <c>item in container</c>: a substring of text, an element of a list
        /// (by ==), a key of an object; null contains nothing.
        /// </summary>
        public static bool Contains(object? container, object? item) => Unwrap(container) switch
        {
            null => false,
            string s => s.Contains(Format(item), StringComparison.Ordinal),
            IDictionary<string, object?> dict => dict.ContainsKey(Format(item)),
            IEnumerable e => e.Cast<object?>().Any(x => AreEqual(x, item)),
            var v => throw new InvalidOperationException($"{Describe(v)} cannot contain values.")
        };

        /// <summary>Ordering of two numbers or two strings; anything else is an error.</summary>
        public static int Compare(object? left, object? right)
        {
            left = Unwrap(left);
            right = Unwrap(right);

            if (left is string ls && right is string rs)
                return string.CompareOrdinal(ls, rs);
            if (TryNumber(left, out var ln) && TryNumber(right, out var rn))
                return ln.CompareTo(rn);
            throw new InvalidOperationException($"Cannot order {Describe(left)} and {Describe(right)}.");
        }

        /// <summary>The value as T: string by Format, bool by truthiness, numbers by value.</summary>
        public static T ConvertTo<T>(object? value)
        {
            var type = typeof(T);
            if (type == typeof(object))
                return Unwrap(value) is T unwrapped
                    ? unwrapped
                    : throw new InvalidOperationException("The expression gave null where a value was expected.");
            if (type == typeof(string))
                return (T)(object)Format(value);
            if (type == typeof(bool))
                return (T)(object)ToBool(value);
            if (type == typeof(double))
                return (T)(object)ToNumber(value);
            if (type == typeof(int))
                return (T)(object)checked((int)Math.Round(ToNumber(value)));
            if (type == typeof(long))
                return (T)(object)checked((long)Math.Round(ToNumber(value)));
            if (type == typeof(decimal))
                return (T)(object)(decimal)ToNumber(value);
            return Convert.ChangeType(Unwrap(value), type, CultureInfo.InvariantCulture) is T converted
                ? converted
                : throw new InvalidCastException($"Cannot convert {Describe(value)} to {type.Name}.");
        }

        internal static string Describe(object? value) => value switch
        {
            null => "null",
            string s => $"'{(s.Length > 20 ? s[..20] + "…" : s)}'",
            _ => $"{Format(value)} ({value.GetType().Name})"
        };
    }
}
