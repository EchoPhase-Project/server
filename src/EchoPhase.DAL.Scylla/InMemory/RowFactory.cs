// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using Cassandra;

namespace EchoPhase.DAL.Scylla.InMemory
{
    /// <summary>
    /// Driver <see cref="Row"/>s from values, so results of the in-memory
    /// engine are read exactly like the cluster's (same mapper, same GetValue).
    /// </summary>
    internal static class RowFactory
    {
        private static readonly ConstructorInfo Constructor = typeof(Row).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(object[]), typeof(CqlColumn[]), typeof(Dictionary<string, int>)], null)
            ?? throw new InvalidOperationException("This driver version has no Row(object[], CqlColumn[], Dictionary) constructor.");

        public sealed record Column(string Name, CqlType Type, string Keyspace = "", string Table = "");

        public static CqlColumn[] Columns(IReadOnlyList<Column> columns)
            => columns.Select((c, i) => new CqlColumn
            {
                Name = c.Name,
                Index = i,
                Type = c.Type.ClrType,
                TypeCode = c.Type.Code,
                Keyspace = c.Keyspace,
                Table = c.Table,
            }).ToArray();

        public static Row Create(CqlColumn[] columns, object?[] values)
        {
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < columns.Length; i++)
                indexes[columns[i].Name] = i;
            return (Row)Constructor.Invoke([values, columns, indexes]);
        }
    }
}
