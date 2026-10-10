// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using System.Numerics;
using Cassandra;

namespace EchoPhase.DAL.Scylla.Schema
{
    /// <summary>
    /// How CLR types map to CQL types, following the DataStax driver's
    /// serializers. Used to generate column types and to check a model against
    /// an existing table.
    /// </summary>
    public static class CqlTypeMap
    {
        private static readonly Dictionary<Type, string[]> Scalars = new()
        {
            // First entry is the canonical type for generated DDL.
            [typeof(string)] = ["text", "varchar", "ascii"],
            [typeof(Guid)] = ["uuid", "timeuuid"],
            [typeof(TimeUuid)] = ["timeuuid"],
            [typeof(int)] = ["int"],
            [typeof(long)] = ["bigint", "counter"],
            [typeof(short)] = ["smallint"],
            [typeof(sbyte)] = ["tinyint"],
            [typeof(bool)] = ["boolean"],
            [typeof(float)] = ["float"],
            [typeof(double)] = ["double"],
            [typeof(decimal)] = ["decimal"],
            [typeof(BigInteger)] = ["varint"],
            [typeof(DateTime)] = ["timestamp"],
            [typeof(DateTimeOffset)] = ["timestamp"],
            [typeof(LocalDate)] = ["date"],
            [typeof(LocalTime)] = ["time"],
            [typeof(Duration)] = ["duration"],
            [typeof(byte[])] = ["blob"],
            [typeof(IPAddress)] = ["inet"],
        };

        /// <summary>The CQL type to declare for <paramref name="clrType"/>.</summary>
        /// <exception cref="NotSupportedException">No known mapping.</exception>
        public static string For(Type clrType)
        {
            var type = Nullable.GetUnderlyingType(clrType) ?? clrType;

            if (Scalars.TryGetValue(type, out var names))
                return names[0];

            if (TryGetCollection(type, out var kind, out var arguments))
                return $"{kind}<{string.Join(", ", arguments.Select(For))}>";

            throw new NotSupportedException($"No CQL type mapping for {clrType.Name}; declare the column type explicitly.");
        }

        /// <summary>
        /// Whether a column of CQL type <paramref name="cqlType"/> (as reported by
        /// system_schema) can be read into <paramref name="clrType"/>.
        /// </summary>
        public static bool IsCompatible(Type clrType, string cqlType)
        {
            var type = Nullable.GetUnderlyingType(clrType) ?? clrType;
            var normalized = Normalize(cqlType);

            if (Scalars.TryGetValue(type, out var names))
                return names.Contains(normalized);

            if (TryGetCollection(type, out var kind, out var arguments))
            {
                if (!normalized.StartsWith(kind + "<") || !normalized.EndsWith('>'))
                    return false;

                var inner = SplitArguments(normalized[(kind.Length + 1)..^1]);
                return inner.Count == arguments.Length
                       && arguments.Zip(inner).All(pair => IsCompatible(pair.First, pair.Second));
            }

            // Unknown types (UDTs, tuples): assume the user mapped them on purpose.
            return true;
        }

        private static bool TryGetCollection(Type type, out string kind, out Type[] arguments)
        {
            kind = string.Empty;
            arguments = [];

            if (type == typeof(string) || type == typeof(byte[]) || !type.IsGenericType)
                return false;

            var definition = type.GetGenericTypeDefinition();
            arguments = type.GetGenericArguments();

            if (definition == typeof(Dictionary<,>) || definition == typeof(IDictionary<,>)
                || definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(SortedDictionary<,>))
                kind = "map";
            else if (definition == typeof(HashSet<>) || definition == typeof(ISet<>)
                     || definition == typeof(IReadOnlySet<>) || definition == typeof(SortedSet<>))
                kind = "set";
            else if (definition == typeof(List<>) || definition == typeof(IList<>) || definition == typeof(IEnumerable<>)
                     || definition == typeof(IReadOnlyList<>) || definition == typeof(ICollection<>))
                kind = "list";
            else
                return false;

            return true;
        }

        // system_schema reports e.g. "frozen<list<text>>" and "map<text, int>".
        private static string Normalize(string cqlType)
        {
            var value = cqlType.Replace(" ", string.Empty).ToLowerInvariant();
            while (value.StartsWith("frozen<") && value.EndsWith('>'))
                value = value["frozen<".Length..^1];
            return value;
        }

        private static List<string> SplitArguments(string value)
        {
            var parts = new List<string>();
            var depth = 0;
            var start = 0;

            for (var i = 0; i < value.Length; i++)
            {
                switch (value[i])
                {
                    case '<': depth++; break;
                    case '>': depth--; break;
                    case ',' when depth == 0:
                        parts.Add(value[start..i]);
                        start = i + 1;
                        break;
                }
            }

            parts.Add(value[start..]);
            return parts;
        }
    }
}
