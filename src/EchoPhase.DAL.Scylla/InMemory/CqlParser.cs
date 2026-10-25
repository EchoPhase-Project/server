// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Text;
using Cassandra;

namespace EchoPhase.DAL.Scylla.InMemory
{
    /// <summary>
    /// Reads the CQL the ORM sends (DDL from migrations; SELECT, INSERT,
    /// UPDATE, DELETE with LWT) into statements. What it does not know fails
    /// loudly, so a test never passes on CQL the cluster would refuse.
    /// </summary>
    internal sealed class CqlParser
    {
        private readonly List<string> _tokens;
        private readonly string _cql;
        private int _position;
        private int _markers;

        private CqlParser(string cql)
        {
            _cql = cql;
            _tokens = Tokenize(cql);
        }

        /// <summary>The statement and how many ? markers it has.</summary>
        public static (Statement Statement, int Markers) Parse(string cql)
        {
            var parser = new CqlParser(cql);
            var statement = parser.Statement();
            parser.Accept(";");
            if (!parser.AtEnd)
                throw parser.Error($"unexpected '{parser.Peek}'");
            return (statement, parser._markers);
        }

        #region Tokens

        private static List<string> Tokenize(string cql)
        {
            var tokens = new List<string>();
            var i = 0;
            while (i < cql.Length)
            {
                var c = cql[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                }
                else if (c == '-' && i + 1 < cql.Length && cql[i + 1] == '-')
                {
                    while (i < cql.Length && cql[i] != '\n') i++;
                }
                else if (c == '\'')
                {
                    var sb = new StringBuilder("'");
                    i++;
                    while (true)
                    {
                        if (i >= cql.Length)
                            throw new SyntaxError("Unterminated string literal.");
                        if (cql[i] == '\'')
                        {
                            if (i + 1 < cql.Length && cql[i + 1] == '\'')
                            {
                                sb.Append('\'');
                                i += 2;
                                continue;
                            }
                            i++;
                            break;
                        }
                        sb.Append(cql[i++]);
                    }
                    tokens.Add(sb.ToString());
                }
                else if (c == '"')
                {
                    var end = cql.IndexOf('"', i + 1);
                    if (end < 0)
                        throw new SyntaxError("Unterminated quoted name.");
                    tokens.Add(cql[(i + 1)..end]);
                    i = end + 1;
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    var start = i;
                    while (i < cql.Length && (char.IsLetterOrDigit(cql[i]) || cql[i] == '_')) i++;
                    tokens.Add(cql[start..i]);
                }
                else if (char.IsDigit(c) || (c == '-' && i + 1 < cql.Length && char.IsDigit(cql[i + 1])))
                {
                    var start = i++;
                    while (i < cql.Length && (char.IsLetterOrDigit(cql[i]) || cql[i] is '.' or '-' || (cql[i] == '+' && cql[i - 1] is 'e' or 'E'))) i++;
                    tokens.Add(cql[start..i]);
                }
                else if (i + 1 < cql.Length && (cql.Substring(i, 2) is "<=" or ">=" or "!="))
                {
                    tokens.Add(cql.Substring(i, 2));
                    i += 2;
                }
                else if ("(),;?=<>*.[]{}:+-".Contains(c))
                {
                    tokens.Add(c.ToString());
                    i++;
                }
                else
                {
                    throw new SyntaxError($"Unexpected character '{c}' in: {cql}");
                }
            }
            return tokens;
        }

        private bool AtEnd => _position >= _tokens.Count;

        private string Peek => AtEnd ? string.Empty : _tokens[_position];

        private bool Is(string word, int ahead = 0)
            => _position + ahead < _tokens.Count
               && string.Equals(_tokens[_position + ahead], word, StringComparison.OrdinalIgnoreCase);

        private bool Accept(string word)
        {
            if (!Is(word))
                return false;
            _position++;
            return true;
        }

        private void Expect(string word)
        {
            if (!Accept(word))
                throw Error($"expected '{word}', found '{Peek}'");
        }

        private string Next()
        {
            if (AtEnd)
                throw Error("unexpected end");
            return _tokens[_position++];
        }

        private string Name()
        {
            var name = Next();
            if (name.StartsWith('\'') || name.Length == 0 || (!char.IsLetter(name[0]) && name[0] != '_' && name != "[applied]"))
                throw Error($"expected a name, found '{name}'");
            return name.ToLowerInvariant();
        }

        private TableName Table()
        {
            var first = Name();
            if (Accept("."))
                return new TableName(first, Name());
            return new TableName(null, first);
        }

        private SyntaxError Error(string what) => new($"Line 1: {what} in: {_cql}");

        #endregion

        #region Statements

        private Statement Statement()
        {
            if (Accept("SELECT"))
                return SelectStatement();
            if (Accept("INSERT"))
                return InsertStatement();
            if (Accept("UPDATE"))
                return UpdateStatement();
            if (Accept("DELETE"))
                return DeleteStatement();
            if (Accept("CREATE"))
                return CreateStatement();
            if (Accept("ALTER"))
                return AlterStatement();
            if (Accept("DROP"))
                return DropStatement();
            if (Accept("USE"))
            {
                Name();
                return new NoOp();
            }
            if (Accept("TRUNCATE"))
            {
                Accept("TABLE");
                return new AlterTable(Table(), "truncate", null, null, null);
            }
            throw Error($"unsupported statement '{Peek}'");
        }

        private Statement CreateStatement()
        {
            if (Accept("KEYSPACE"))
            {
                SkipRest();
                return new NoOp();
            }
            if (Accept("TYPE"))
            {
                SkipRest();
                return new NoOp();
            }
            if (Accept("INDEX"))
            {
                var ifNotExists = Accept("IF") && Accept("NOT") && Accept("EXISTS");
                string? name = Is("ON") ? null : Name();
                Expect("ON");
                var table = Table();
                Expect("(");
                var column = Name();
                Expect(")");
                return new CreateIndex(name, table, column, ifNotExists);
            }
            if (Accept("MATERIALIZED"))
                throw Error("materialized views are not supported by the in-memory engine");
            Expect("TABLE");
            return CreateTableStatement();
        }

        private Statement CreateTableStatement()
        {
            var ifNotExists = false;
            if (Accept("IF"))
            {
                Expect("NOT");
                Expect("EXISTS");
                ifNotExists = true;
            }
            var table = Table();
            Expect("(");
            var columns = new List<ColumnDefinition>();
            var partition = new List<string>();
            var clustering = new List<string>();
            do
            {
                if (Accept("PRIMARY"))
                {
                    Expect("KEY");
                    Expect("(");
                    if (Accept("("))
                    {
                        do partition.Add(Name()); while (Accept(","));
                        Expect(")");
                    }
                    else
                    {
                        partition.Add(Name());
                    }
                    while (Accept(","))
                        clustering.Add(Name());
                    Expect(")");
                    continue;
                }

                var name = Name();
                var type = TypeText();
                var isStatic = Accept("STATIC");
                if (Accept("PRIMARY"))
                {
                    Expect("KEY");
                    partition.Add(name);
                }
                columns.Add(new ColumnDefinition(name, CqlType.Parse(type), isStatic));
            }
            while (Accept(","));
            Expect(")");

            var descending = new Dictionary<string, bool>(StringComparer.Ordinal);
            int? defaultTtl = null;
            if (Accept("WITH"))
            {
                do
                {
                    if (Accept("CLUSTERING"))
                    {
                        Expect("ORDER");
                        Expect("BY");
                        Expect("(");
                        do
                        {
                            var column = Name();
                            descending[column] = Accept("DESC") || (!Accept("ASC") && false);
                        }
                        while (Accept(","));
                        Expect(")");
                        continue;
                    }
                    if (Accept("COMPACT"))
                    {
                        Expect("STORAGE");
                        continue;
                    }
                    var option = Name();
                    Expect("=");
                    var value = OptionValue();
                    if (option == "default_time_to_live")
                        defaultTtl = int.Parse(value, CultureInfo.InvariantCulture);
                }
                while (Accept("AND"));
            }

            if (partition.Count == 0)
                throw new InvalidQueryException($"Table {table.Name} has no PRIMARY KEY.");
            foreach (var key in partition.Concat(clustering))
                if (columns.All(c => c.Name != key))
                    throw new InvalidQueryException($"Unknown definition {key} referenced in PRIMARY KEY.");
            if (columns.Any(c => c.Static))
                throw new NotSupportedException("Static columns are not supported by the in-memory engine.");

            return new CreateTable(table, ifNotExists, columns, partition, clustering, descending, defaultTtl);
        }

        // A type as text, nested generics included: "map<text, frozen<list<int>>>".
        private string TypeText()
        {
            var sb = new StringBuilder(Name());
            if (Accept("<"))
            {
                sb.Append('<').Append(TypeText());
                while (Accept(","))
                    sb.Append(',').Append(TypeText());
                Expect(">");
                sb.Append('>');
            }
            return sb.ToString();
        }

        // An option's value, skipped over: a number, a string or a {map}.
        private string OptionValue()
        {
            if (Accept("{"))
            {
                var depth = 1;
                while (depth > 0)
                {
                    var token = Next();
                    if (token == "{") depth++;
                    else if (token == "}") depth--;
                }
                return string.Empty;
            }
            return Next().Trim('\'');
        }

        private void SkipRest()
        {
            while (!AtEnd && Peek != ";")
                _position++;
        }

        private Statement AlterStatement()
        {
            if (Accept("KEYSPACE") || Accept("TYPE"))
            {
                SkipRest();
                return new NoOp();
            }
            Expect("TABLE");
            var table = Table();
            if (Accept("ADD"))
            {
                var column = Name();
                return new AlterTable(table, "add", column, CqlType.Parse(TypeText()), null);
            }
            if (Accept("DROP"))
                return new AlterTable(table, "drop", Name(), null, null);
            if (Accept("RENAME"))
            {
                var from = Name();
                Expect("TO");
                return new AlterTable(table, "rename", from, null, Name());
            }
            if (Accept("WITH"))
            {
                var option = Name();
                Expect("=");
                var value = OptionValue();
                return option == "default_time_to_live"
                    ? new AlterTable(table, "ttl", value, null, null)
                    : new NoOp();
            }
            throw Error($"unsupported ALTER TABLE '{Peek}'");
        }

        private Statement DropStatement()
        {
            if (Accept("KEYSPACE") || Accept("TYPE"))
            {
                SkipRest();
                return new NoOp();
            }
            if (Accept("INDEX"))
            {
                var ifExists = Accept("IF") && Accept("EXISTS");
                return new DropIndex(Table(), ifExists);
            }
            Expect("TABLE");
            var exists = false;
            if (Accept("IF"))
            {
                Expect("EXISTS");
                exists = true;
            }
            return new DropTable(Table(), exists);
        }

        private Statement InsertStatement()
        {
            Expect("INTO");
            var table = Table();
            Expect("(");
            var columns = new List<string>();
            do columns.Add(Name()); while (Accept(","));
            Expect(")");
            Expect("VALUES");
            Expect("(");
            var values = new List<Term>();
            do values.Add(TermValue()); while (Accept(","));
            Expect(")");
            if (columns.Count != values.Count)
                throw new InvalidQueryException("Unmatched column names/values.");

            var ifNotExists = false;
            WriteUsing usingClause = new(null, null);
            // Both orders occur: IF NOT EXISTS USING TTL, USING TTL ... IF NOT EXISTS.
            for (var i = 0; i < 2; i++)
            {
                if (Accept("IF"))
                {
                    Expect("NOT");
                    Expect("EXISTS");
                    ifNotExists = true;
                }
                else if (Is("USING"))
                {
                    usingClause = Using();
                }
            }
            return new Insert(table, columns, values, usingClause, ifNotExists);
        }

        private WriteUsing Using()
        {
            Term? ttl = null;
            Term? timestamp = null;
            if (!Accept("USING"))
                return new WriteUsing(null, null);
            do
            {
                if (Accept("TTL"))
                    ttl = TermValue();
                else if (Accept("TIMESTAMP"))
                    timestamp = TermValue();
                else
                    throw Error($"expected TTL or TIMESTAMP, found '{Peek}'");
            }
            while (Accept("AND"));
            return new WriteUsing(ttl, timestamp);
        }

        private Statement UpdateStatement()
        {
            var table = Table();
            var usingClause = Using();
            Expect("SET");
            var assignments = new List<Assignment>();
            do
            {
                var column = Name();
                if (Accept("["))
                {
                    var key = TermValue();
                    Expect("]");
                    Expect("=");
                    assignments.Add(new Assignment(column, "element", TermValue(), key));
                    continue;
                }
                Expect("=");
                if (Is(column) && (Is("+", 1) || Is("-", 1)))
                {
                    Next();
                    var op = Next();
                    assignments.Add(new Assignment(column, op == "+" ? "add" : "remove", TermValue()));
                    continue;
                }
                var value = TermValue();
                if (Accept("+"))
                {
                    if (Name() != column)
                        throw Error("expected the same column after +");
                    assignments.Add(new Assignment(column, "prepend", value));
                    continue;
                }
                assignments.Add(new Assignment(column, "set", value));
            }
            while (Accept(","));
            Expect("WHERE");
            var where = Relations();
            return new Update(table, usingClause, assignments, where, IfClause());
        }

        private Statement DeleteStatement()
        {
            var columns = new List<string>();
            if (!Is("FROM"))
            {
                do
                {
                    columns.Add(Name());
                    if (Accept("["))
                        throw Error("deleting a collection element is not supported by the in-memory engine");
                }
                while (Accept(","));
            }
            Expect("FROM");
            var table = Table();
            var usingClause = Using();
            Expect("WHERE");
            var where = Relations();
            return new Delete(table, columns, usingClause, where, IfClause());
        }

        private Condition IfClause()
        {
            if (!Accept("IF"))
                return Condition.None;
            if (Accept("EXISTS"))
                return new Condition(true, false, []);
            if (Accept("NOT"))
            {
                Expect("EXISTS");
                return new Condition(false, true, []);
            }
            return new Condition(false, false, Relations());
        }

        private Statement SelectStatement()
        {
            List<string>? columns = null;
            var count = false;
            if (Accept("*"))
            {
            }
            else if (Is("COUNT") && Is("(", 1))
            {
                Next();
                Expect("(");
                if (!Accept("*"))
                    Expect("1");
                Expect(")");
                count = true;
            }
            else
            {
                columns = [];
                do columns.Add(Name()); while (Accept(","));
            }
            Expect("FROM");
            var table = Table();
            var where = Accept("WHERE") ? Relations() : [];
            var order = new List<OrderBy>();
            if (Accept("ORDER"))
            {
                Expect("BY");
                do
                {
                    var column = Name();
                    var descending = Accept("DESC");
                    if (!descending)
                        Accept("ASC");
                    order.Add(new OrderBy(column, descending));
                }
                while (Accept(","));
            }
            Term? limit = Accept("LIMIT") ? TermValue() : null;
            var allowFiltering = false;
            if (Accept("ALLOW"))
            {
                Expect("FILTERING");
                allowFiltering = true;
            }
            return new Select(table, columns, count, where, order, limit, allowFiltering);
        }

        private List<Relation> Relations()
        {
            var relations = new List<Relation>();
            do relations.Add(RelationClause()); while (Accept("AND"));
            return relations;
        }

        private Relation RelationClause()
        {
            var column = Name();
            if (Accept("IN"))
            {
                if (Accept("("))
                {
                    var items = new List<Term>();
                    if (!Is(")"))
                        do items.Add(TermValue()); while (Accept(","));
                    Expect(")");
                    return new Relation(column, Operator.In, new ListLiteral(items));
                }
                return new Relation(column, Operator.In, TermValue());
            }
            if (Accept("CONTAINS"))
                return new Relation(column, Accept("KEY") ? Operator.ContainsKey : Operator.Contains, TermValue());

            var op = Next() switch
            {
                "=" => Operator.Eq,
                "<" => Operator.Lt,
                "<=" => Operator.Le,
                ">" => Operator.Gt,
                ">=" => Operator.Ge,
                "!=" => Operator.Ne,
                var other => throw Error($"unsupported operator '{other}'"),
            };
            return new Relation(column, op, TermValue());
        }

        private Term TermValue()
        {
            var token = Next();
            if (token == "?")
                return new Marker(_markers++);
            if (token == "[")
            {
                var items = new List<Term>();
                if (!Is("]"))
                    do items.Add(TermValue()); while (Accept(","));
                Expect("]");
                return new ListLiteral(items);
            }
            if (token == "{")
            {
                if (Accept("}"))
                    return new SetLiteral([]);
                var first = TermValue();
                if (Accept(":"))
                {
                    var pairs = new List<(Term, Term)> { (first, TermValue()) };
                    while (Accept(","))
                    {
                        var key = TermValue();
                        Expect(":");
                        pairs.Add((key, TermValue()));
                    }
                    Expect("}");
                    return new MapLiteral(pairs);
                }
                var set = new List<Term> { first };
                while (Accept(","))
                    set.Add(TermValue());
                Expect("}");
                return new SetLiteral(set);
            }
            if (token.StartsWith('\''))
                return new Literal(token[1..]);
            if (string.Equals(token, "null", StringComparison.OrdinalIgnoreCase))
                return new Literal(null);
            if (string.Equals(token, "true", StringComparison.OrdinalIgnoreCase))
                return new Literal(true);
            if (string.Equals(token, "false", StringComparison.OrdinalIgnoreCase))
                return new Literal(false);
            if (Guid.TryParse(token, out var guid))
                return new Literal(guid);
            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                return new Literal(integer);
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
                return new Literal(real);
            throw Error($"expected a value, found '{token}'");
        }

        #endregion
    }
}
