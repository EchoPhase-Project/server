// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Net;
using System.Numerics;
using Cassandra;

namespace EchoPhase.DAL.Scylla.InMemory
{
    /// <summary>
    /// A CQL column type as the engine understands it: the native ones and
    /// list, set, map, frozen. Converts bound values to the .NET type the
    /// driver returns, and orders values as Scylla does.
    /// </summary>
    internal sealed record CqlType(string Name, IReadOnlyList<CqlType> Arguments)
    {
        private static readonly Dictionary<string, (Type Clr, ColumnTypeCode Code)> Natives = new(StringComparer.Ordinal)
        {
            ["ascii"] = (typeof(string), ColumnTypeCode.Ascii),
            ["text"] = (typeof(string), ColumnTypeCode.Text),
            ["varchar"] = (typeof(string), ColumnTypeCode.Varchar),
            ["uuid"] = (typeof(Guid), ColumnTypeCode.Uuid),
            ["timeuuid"] = (typeof(Guid), ColumnTypeCode.Timeuuid),
            ["int"] = (typeof(int), ColumnTypeCode.Int),
            ["bigint"] = (typeof(long), ColumnTypeCode.Bigint),
            ["counter"] = (typeof(long), ColumnTypeCode.Counter),
            ["smallint"] = (typeof(short), ColumnTypeCode.SmallInt),
            ["tinyint"] = (typeof(sbyte), ColumnTypeCode.TinyInt),
            ["boolean"] = (typeof(bool), ColumnTypeCode.Boolean),
            ["float"] = (typeof(float), ColumnTypeCode.Float),
            ["double"] = (typeof(double), ColumnTypeCode.Double),
            ["decimal"] = (typeof(decimal), ColumnTypeCode.Decimal),
            ["varint"] = (typeof(BigInteger), ColumnTypeCode.Varint),
            ["timestamp"] = (typeof(DateTimeOffset), ColumnTypeCode.Timestamp),
            ["date"] = (typeof(LocalDate), ColumnTypeCode.Date),
            ["time"] = (typeof(LocalTime), ColumnTypeCode.Time),
            ["duration"] = (typeof(Duration), ColumnTypeCode.Duration),
            ["blob"] = (typeof(byte[]), ColumnTypeCode.Blob),
            ["inet"] = (typeof(IPAddress), ColumnTypeCode.Inet),
        };

        public static CqlType Of(string name, params CqlType[] arguments) => new(name, arguments);

        public bool IsCollection => Name is "list" or "set" or "map";

        public bool IsCounter => Name == "counter";

        /// <summary>The type without frozen&lt;&gt;.</summary>
        public CqlType Unfrozen => Name == "frozen" ? Arguments[0].Unfrozen : this;

        public Type ClrType => Unfrozen switch
        {
            { Name: "list" or "set" } t => t.Arguments[0].ClrType.MakeArrayType(),
            { Name: "map" } t => typeof(SortedDictionary<,>).MakeGenericType(t.Arguments[0].ClrType, t.Arguments[1].ClrType),
            var t when Natives.TryGetValue(t.Name, out var native) => native.Clr,
            var t => throw new NotSupportedException($"The in-memory engine has no type '{t.Name}' (user types are not supported)."),
        };

        public ColumnTypeCode Code => Unfrozen switch
        {
            { Name: "list" } => ColumnTypeCode.List,
            { Name: "set" } => ColumnTypeCode.Set,
            { Name: "map" } => ColumnTypeCode.Map,
            var t => Natives.TryGetValue(t.Name, out var native) ? native.Code : ColumnTypeCode.Custom,
        };

        /// <summary>As system_schema writes it: "text", "map&lt;text, int&gt;".</summary>
        public override string ToString()
            => Arguments.Count == 0 ? Name : $"{Name}<{string.Join(", ", Arguments)}>";

        public static CqlType Parse(string text)
        {
            var position = 0;
            var type = Read(text.Replace(" ", string.Empty).ToLowerInvariant(), ref position);
            if (position != text.Replace(" ", string.Empty).Length)
                throw new InvalidQueryException($"Unknown type '{text}'.");
            return type;
        }

        private static CqlType Read(string text, ref int position)
        {
            var start = position;
            while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] == '_'))
                position++;
            var name = text[start..position];
            if (name.Length == 0)
                throw new InvalidQueryException($"Expected a type at '{text[start..]}'.");

            var arguments = new List<CqlType>();
            if (position < text.Length && text[position] == '<')
            {
                position++;
                arguments.Add(Read(text, ref position));
                while (text[position] == ',')
                {
                    position++;
                    arguments.Add(Read(text, ref position));
                }
                if (text[position] != '>')
                    throw new InvalidQueryException($"Expected '>' in type '{text}'.");
                position++;
            }

            var type = new CqlType(name, arguments);
            _ = type.ClrType; // unknown types fail on CREATE TABLE, not on first use
            return type;
        }

        /// <summary>
        /// A bound or literal value as the driver would hand it back; null stays
        /// null. Fails, as Scylla does, on a value of the wrong type.
        /// </summary>
        public object? Normalize(object? value)
        {
            if (value is null)
                return null;

            var type = Unfrozen;
            try
            {
                switch (type.Name)
                {
                    case "ascii" or "text" or "varchar":
                        return value as string ?? throw Mismatch(value);
                    case "uuid" or "timeuuid":
                        return value switch
                        {
                            Guid g => g,
                            TimeUuid t => t.ToGuid(),
                            string s => Guid.Parse(s),
                            _ => throw Mismatch(value),
                        };
                    case "int":
                        return Convert.ToInt32(Number(value), CultureInfo.InvariantCulture);
                    case "bigint" or "counter":
                        return Convert.ToInt64(Number(value), CultureInfo.InvariantCulture);
                    case "smallint":
                        return Convert.ToInt16(Number(value), CultureInfo.InvariantCulture);
                    case "tinyint":
                        return Convert.ToSByte(Number(value), CultureInfo.InvariantCulture);
                    case "boolean":
                        return value as bool? ?? throw Mismatch(value);
                    case "float":
                        return Convert.ToSingle(Number(value), CultureInfo.InvariantCulture);
                    case "double":
                        return Convert.ToDouble(Number(value), CultureInfo.InvariantCulture);
                    case "decimal":
                        return Convert.ToDecimal(Number(value), CultureInfo.InvariantCulture);
                    case "varint":
                        return value is BigInteger b ? b : new BigInteger(Convert.ToDecimal(Number(value), CultureInfo.InvariantCulture));
                    case "timestamp":
                        // Scylla keeps milliseconds: more precision is lost on the way.
                        var moment = value switch
                        {
                            DateTimeOffset o => o,
                            DateTime d => new DateTimeOffset(d.Kind == DateTimeKind.Unspecified
                                ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
                                : d.ToUniversalTime()),
                            long ms => DateTimeOffset.FromUnixTimeMilliseconds(ms),
                            string s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture),
                            _ => throw Mismatch(value),
                        };
                        return DateTimeOffset.FromUnixTimeMilliseconds(moment.ToUnixTimeMilliseconds());
                    case "date":
                        return value as LocalDate ?? throw Mismatch(value);
                    case "time":
                        return value as LocalTime ?? throw Mismatch(value);
                    case "duration":
                        return value is Duration duration ? duration : throw Mismatch(value);
                    case "blob":
                        return value as byte[] ?? throw Mismatch(value);
                    case "inet":
                        return value as IPAddress ?? throw Mismatch(value);
                    case "list":
                        return TypedArray(type.Arguments[0], Items(value).Select(type.Arguments[0].Normalize));
                    case "set":
                        var element = type.Arguments[0];
                        var distinct = new List<object?>();
                        foreach (var item in Items(value).Select(element.Normalize).OrderBy(x => x, element.Comparer))
                            if (distinct.Count == 0 || element.Compare(distinct[^1], item) != 0)
                                distinct.Add(item);
                        return TypedArray(element, distinct);
                    case "map":
                        if (value is not System.Collections.IDictionary pairs)
                            throw Mismatch(value);
                        var map = Reflect.Create<System.Collections.IDictionary>(type.ClrType, type.Arguments[0].Comparer);
                        foreach (System.Collections.DictionaryEntry pair in pairs)
                            map[type.Arguments[0].Normalize(pair.Key) ?? throw new InvalidQueryException("A map key cannot be null.")]
                                = type.Arguments[1].Normalize(pair.Value);
                        return map;
                }
            }
            catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
            {
                throw Mismatch(value);
            }

            throw new NotSupportedException($"The in-memory engine has no type '{type.Name}'.");

            InvalidQueryException Mismatch(object v) =>
                new($"Invalid value {v} ({v.GetType().Name}) for type {this}.");
        }

        private static object Number(object value) => value switch
        {
            bool => throw new InvalidCastException(),
            string s => decimal.Parse(s, CultureInfo.InvariantCulture),
            IConvertible => value,
            BigInteger b => (decimal)b,
            _ => throw new InvalidCastException(),
        };

        private static IEnumerable<object?> Items(object value)
            => value is System.Collections.IEnumerable items and not string
                ? items.Cast<object?>()
                : throw new InvalidQueryException($"Expected a collection, got {value.GetType().Name}.");

        private static Array TypedArray(CqlType element, IEnumerable<object?> items)
        {
            var list = items.ToList();
            var array = Array.CreateInstance(element.ClrType, list.Count);
            for (var i = 0; i < list.Count; i++)
                array.SetValue(list[i], i);
            return array;
        }

        public IComparer<object?> Comparer => Comparer<object?>.Create(Compare);

        /// <summary>Scylla's order of two values of this type; null first.</summary>
        public int Compare(object? a, object? b)
        {
            if (a is null || b is null)
                return a is null ? (b is null ? 0 : -1) : 1;

            return (Unfrozen.Name, a, b) switch
            {
                ("timeuuid", Guid x, Guid y) => CompareTimeUuid(x, y),
                ("uuid", Guid x, Guid y) => CompareUuid(x, y),
                (_, string x, string y) => string.CompareOrdinal(x, y),
                (_, byte[] x, byte[] y) => x.AsSpan().SequenceCompareTo(y),
                (_, IPAddress x, IPAddress y) => x.GetAddressBytes().AsSpan().SequenceCompareTo(y.GetAddressBytes()),
                (_, Array x, Array y) => CompareSequences(x.Cast<object?>(), y.Cast<object?>(), Arguments[0]),
                (_, IComparable x, _) => x.CompareTo(b),
                _ => string.CompareOrdinal(a.ToString(), b.ToString()),
            };
        }

        private static int CompareSequences(IEnumerable<object?> a, IEnumerable<object?> b, CqlType element)
        {
            using var x = a.GetEnumerator();
            using var y = b.GetEnumerator();
            while (true)
            {
                var hasX = x.MoveNext();
                var hasY = y.MoveNext();
                if (!hasX || !hasY)
                    return hasX.CompareTo(hasY);
                var c = element.Compare(x.Current, y.Current);
                if (c != 0)
                    return c;
            }
        }

        // UUIDType: by version, then time for version 1, then the bytes (unsigned).
        private static int CompareUuid(Guid x, Guid y)
        {
            var bx = x.ToByteArray(bigEndian: true);
            var by = y.ToByteArray(bigEndian: true);
            var c = (bx[6] >> 4).CompareTo(by[6] >> 4);
            if (c != 0)
                return c;
            if (bx[6] >> 4 == 1)
            {
                c = TimeOf(bx).CompareTo(TimeOf(by));
                if (c != 0)
                    return c;
            }
            return bx.AsSpan().SequenceCompareTo(by);
        }

        private static int CompareTimeUuid(Guid x, Guid y)
        {
            var bx = x.ToByteArray(bigEndian: true);
            var by = y.ToByteArray(bigEndian: true);
            var c = TimeOf(bx).CompareTo(TimeOf(by));
            return c != 0 ? c : bx.AsSpan().SequenceCompareTo(by);
        }

        private static long TimeOf(byte[] b)
            => ((long)(b[6] & 0x0F) << 56) | ((long)b[7] << 48) | ((long)b[4] << 40) | ((long)b[5] << 32)
               | ((long)b[0] << 24) | ((long)b[1] << 16) | ((long)b[2] << 8) | b[3];
    }
}
