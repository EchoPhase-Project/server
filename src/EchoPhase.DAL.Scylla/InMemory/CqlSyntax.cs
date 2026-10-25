// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.InMemory
{
    /// <summary>A value in a statement: a bind marker, a literal or a collection literal.</summary>
    internal abstract record Term;

    internal sealed record Marker(int Index) : Term;

    internal sealed record Literal(object? Value) : Term;

    internal sealed record ListLiteral(IReadOnlyList<Term> Items) : Term;

    internal sealed record SetLiteral(IReadOnlyList<Term> Items) : Term;

    internal sealed record MapLiteral(IReadOnlyList<(Term Key, Term Value)> Pairs) : Term;

    internal enum Operator
    {
        Eq,
        Lt,
        Le,
        Gt,
        Ge,
        Ne,
        In,
        Contains,
        ContainsKey,
    }

    /// <summary>column op term, in WHERE or in an IF condition. IN holds a list (literal or bound).</summary>
    internal sealed record Relation(string Column, Operator Op, Term Value);

    internal sealed record TableName(string? Keyspace, string Name);

    internal sealed record WriteUsing(Term? Ttl, Term? Timestamp);

    /// <summary>What an LWT checks: nothing, IF EXISTS, IF NOT EXISTS, or IF conditions.</summary>
    internal sealed record Condition(bool IfExists, bool IfNotExists, IReadOnlyList<Relation> Relations)
    {
        public static readonly Condition None = new(false, false, []);

        public bool Any => IfExists || IfNotExists || Relations.Count > 0;
    }

    internal abstract record Statement;

    internal sealed record NoOp : Statement;

    internal sealed record ColumnDefinition(string Name, CqlType Type, bool Static);

    internal sealed record CreateTable(
        TableName Table,
        bool IfNotExists,
        IReadOnlyList<ColumnDefinition> Columns,
        IReadOnlyList<string> PartitionKey,
        IReadOnlyList<string> ClusteringKey,
        IReadOnlyDictionary<string, bool> Descending,
        int? DefaultTtl) : Statement;

    internal sealed record AlterTable(TableName Table, string Action, string? Column, CqlType? Type, string? NewName) : Statement;

    internal sealed record DropTable(TableName Table, bool IfExists) : Statement;

    internal sealed record CreateIndex(string? Name, TableName Table, string Column, bool IfNotExists) : Statement;

    internal sealed record DropIndex(TableName Index, bool IfExists) : Statement;

    internal sealed record Insert(
        TableName Table,
        IReadOnlyList<string> Columns,
        IReadOnlyList<Term> Values,
        WriteUsing Using,
        bool IfNotExists) : Statement;

    /// <summary>
    /// col = term; col = col + term (counters, collections); col = term + col
    /// (list prepend); col = col - term; col[key] = term.
    /// </summary>
    internal sealed record Assignment(string Column, string Kind, Term Value, Term? Key = null);

    internal sealed record Update(
        TableName Table,
        WriteUsing Using,
        IReadOnlyList<Assignment> Assignments,
        IReadOnlyList<Relation> Where,
        Condition If) : Statement;

    internal sealed record Delete(
        TableName Table,
        IReadOnlyList<string> Columns,
        WriteUsing Using,
        IReadOnlyList<Relation> Where,
        Condition If) : Statement;

    internal sealed record OrderBy(string Column, bool Descending);

    /// <summary>Selectors: null for *, ["COUNT(*)"] for a count, else column names.</summary>
    internal sealed record Select(
        TableName Table,
        IReadOnlyList<string>? Columns,
        bool Count,
        IReadOnlyList<Relation> Where,
        IReadOnlyList<OrderBy> OrderBy,
        Term? Limit,
        bool AllowFiltering) : Statement;
}
