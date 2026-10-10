// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Cql
{
    internal static class CqlLiteral
    {
        public static string Quote(string value) => $"'{value.Replace("'", "''")}'";

        public static string Options(string classKey, string className, (string key, string value)[] options) =>
            "{" + string.Join(", ",
                new[] { $"{Quote(classKey)}: {Quote(className)}" }
                    .Concat(options.Select(o => $"{Quote(o.key)}: {Quote(o.value)}"))) + "}";
    }
}
