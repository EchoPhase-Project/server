// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// Pratt parser from lexemes to a <see cref="Node"/> tree. Precedence,
    /// loosest first: =&gt; (lambda), ?: , ||, &&, not (the word), == !=, &lt; &gt; &lt;= &gt;= in, not in, + -, * / %, unary - !.
    /// Literals: numbers, strings, true/false/null, [list], {object} and
    /// [x for x in xs if cond]. After a value: .name, [index], [start:end:step].
    /// <c>for</c>, <c>in</c> and <c>if</c> are keywords only where they can
    /// be, so variables keep those names.
    /// </summary>
    internal sealed class ExpressionParser
    {
        private const int UnaryPower = 70;

        private readonly List<Lexeme> _tokens;
        private int _pos;

        /// <summary>
        /// Nesting the parser (and later the evaluator) recurses through: deep
        /// enough for any real expression, far from the stack's end. Without it
        /// a source of 100 000 '(' would overflow the stack, which ends the
        /// whole process instead of failing one expression.
        /// </summary>
        public const int MaxNesting = 200;

        private int _nesting;

        // Lambdas parsed so far: a body or comprehension that adds none holds none.
        private int _lambdas;

        private ExpressionParser(List<Lexeme> tokens)
        {
            _tokens = tokens;
        }

        public static Node Parse(string source)
        {
            var parser = new ExpressionParser(Tokenizer.Tokenize(source));
            if (parser.Current.Kind == Tk.End)
                throw new ScriptSyntaxException("Empty expression", 0);

            var node = parser.Expression(0);
            if (parser.Current.Kind != Tk.End)
                throw new ScriptSyntaxException($"Unexpected '{parser.Current.Text}'", parser.Current.Position);
            return node;
        }

        /// <summary>A path alone (what Set assigns to): <c>name</c>, <c>a.b</c>, <c>items[i + 1]</c>.</summary>
        public static PathNode ParsePath(string source)
        {
            var parser = new ExpressionParser(Tokenizer.Tokenize(source));
            var first = parser.Advance();
            if (first.Kind != Tk.Identifier)
                throw new ScriptSyntaxException("A path starts with a variable name", first.Position);

            var path = parser.Path(first);
            if (parser.Current.Kind != Tk.End)
                throw new ScriptSyntaxException($"Unexpected '{parser.Current.Text}' in path", parser.Current.Position);
            return path;
        }

        private Lexeme Current => _tokens[_pos];

        private Lexeme Advance() => _tokens[_pos < _tokens.Count - 1 ? _pos++ : _pos];

        private Lexeme Expect(Tk kind, string what)
        {
            if (Current.Kind != kind)
                throw new ScriptSyntaxException(
                    Current.Kind == Tk.End ? $"Expected {what} before the end" : $"Expected {what}, found '{Current.Text}'",
                    Current.Position);
            return Advance();
        }

        private Node Expression(int minPower)
        {
            if (++_nesting > MaxNesting)
                throw new ScriptSyntaxException($"Nested more than {MaxNesting} deep", Current.Position);
            try
            {
                return ExpressionAt(minPower);
            }
            finally
            {
                _nesting--;
            }
        }

        private Node ExpressionAt(int minPower)
        {
            var left = Prefix();

            while (true)
            {
                var op = Current;
                if (op.Kind == Tk.Question)
                {
                    // Right-associative: a ? b : c ? d : e
                    if (5 < minPower)
                        break;
                    Advance();
                    var whenTrue = Expression(0);
                    Expect(Tk.Colon, "':'");
                    var whenFalse = Expression(4);
                    // A constant condition picks its branch now.
                    left = left.IsConstant && left is Literal known
                        ? (Values.ToBool(known.Value) ? whenTrue : whenFalse)
                        : Fold(new Conditional(left, whenTrue, whenFalse));
                    continue;
                }

                // x in xs, x not in xs: comparisons, by word.
                if (IsWord(op, "in") || (op.Kind == Tk.Not && IsWord(op, "not") && IsWord(Peek(1), "in")))
                {
                    if (InPower < minPower)
                        break;
                    var negated = op.Kind == Tk.Not;
                    Advance();
                    if (negated)
                        Advance();
                    left = Fold(new Binary(negated ? BinaryOp.NotIn : BinaryOp.In, left, Expression(InPower + 1)));
                    continue;
                }

                if (Infix(op.Kind) is not var (binary, power) || power < minPower)
                    break;

                Advance();
                left = Fold(new Binary(binary, left, Expression(power + 1)));
            }

            return left;
        }

        private Node Prefix()
        {
            var token = Advance();
            switch (token.Kind)
            {
                case Tk.Number:
                    return new Literal(token.Number);
                case Tk.String:
                    return new Literal(token.Text);
                case Tk.Minus:
                    return Fold(new Unary(UnaryOp.Negate, Expression(UnaryPower)));
                // Python's word: not a == b is not (a == b); '!' binds tightly, as in C.
                case Tk.Not when IsWord(token, "not"):
                    return Fold(new Unary(UnaryOp.Not, Expression(NotPower)));
                case Tk.Not:
                    return Fold(new Unary(UnaryOp.Not, Expression(UnaryPower)));
                case Tk.LParen when IsLambdaAhead():
                    return Lambda(Parameters());
                case Tk.LParen:
                    var inner = Expression(0);
                    Expect(Tk.RParen, "')'");
                    return Postfix(inner);
                case Tk.LBracket:
                    return Postfix(ListOrComprehension());
                case Tk.LBrace:
                    return Postfix(FoldObject([.. Items(Tk.RBrace, "'}'", Member)]));
                case Tk.Identifier when Current.Kind == Tk.Arrow:
                    return Lambda([Parameter(token)]);
                case Tk.Identifier:
                    switch (token.Text.ToLowerInvariant())
                    {
                        case "true": return new Literal(true);
                        case "false": return new Literal(false);
                        case "null": return new Literal(null);
                    }
                    return Current.Kind == Tk.LParen ? Postfix(Fold(Call(token))) : Path(token);
                case Tk.End:
                    throw new ScriptSyntaxException("Unexpected end of expression", token.Position);
                default:
                    throw new ScriptSyntaxException($"Unexpected '{token.Text}'", token.Position);
            }
        }

        private Node Call(Lexeme name)
        {
            Advance(); // (
            var arguments = Items(Tk.RParen, "')'", () => Expression(0));
            var (body, pure) = Functions.Resolve(name.Text, arguments.Count, name.Position);
            return new Call(name.Text, body, pure, [.. arguments]);
        }

        private const int InPower = 40;

        // Above && and ||, below the comparisons.
        private const int NotPower = 30;

        private Lexeme Peek(int ahead) => _tokens[Math.Min(_pos + ahead, _tokens.Count - 1)];

        private static bool IsWord(Lexeme token, string word)
            => token.Kind is Tk.Identifier or Tk.Not && token.Text.Equals(word, StringComparison.OrdinalIgnoreCase);

        // After '[': a list literal, or [element for names in source (for ... | if ...)*].
        private Node ListOrComprehension()
        {
            int lambdasBefore = _lambdas;
            if (Current.Kind == Tk.RBracket)
            {
                Advance();
                return FoldList([]);
            }

            var first = Expression(0);
            if (!IsWord(Current, "for"))
            {
                var items = new List<Node> { first };
                if (Current.Kind == Tk.Comma)
                {
                    Advance();
                    items.AddRange(Items(Tk.RBracket, "']'", () => Expression(0)));
                }
                else
                {
                    Expect(Tk.RBracket, "']'");
                }
                return FoldList([.. items]);
            }

            var clauses = new List<ComprehensionClause>();
            while (true)
            {
                if (IsWord(Current, "for"))
                {
                    Advance();
                    var names = new List<string> { Parameter(Expect(Tk.Identifier, "a name after 'for'")) };
                    while (Current.Kind == Tk.Comma)
                    {
                        Advance();
                        names.Add(Parameter(Expect(Tk.Identifier, "a name")));
                    }
                    if (names.Distinct(StringComparer.Ordinal).Count() != names.Count)
                        throw new ScriptSyntaxException("Names after 'for' must differ", Current.Position);
                    if (!IsWord(Current, "in"))
                        throw new ScriptSyntaxException("Expected 'in'", Current.Position);
                    Advance();
                    // Above 'in', so the source does not swallow a following comparison: for x in xs if x in ys
                    clauses.Add(new ForClause([.. names], Expression(InPower + 1)));
                }
                else if (IsWord(Current, "if"))
                {
                    Advance();
                    clauses.Add(new IfClause(TieredNode.Wrap(Expression(0))));
                }
                else
                {
                    Expect(Tk.RBracket, "']'");
                    return new Comprehension(TieredNode.Wrap(first), [.. clauses], reusable: _lambdas == lambdasBefore);
                }
            }
        }

        private const int FoldOperations = 10_000;
        // What a folded value may weigh: elements at every depth, and characters.
        private const int FoldMaxItems = 64;
        private const int FoldMaxText = 4096;

        private static readonly Dictionary<string, object?> NoVariables = [];

        // A constant expression (60 * 60 * 24, upper('x'), [1, 2][0]) computed
        // now, once, as a literal. Not when it fails (1 / 0 in a branch never
        // taken must not stop a program from saving: it fails when run, as
        // before), nor when its value is large: the cache would keep it.
        private static Node Fold(Node node)
        {
            if (node is Literal || !node.IsConstant)
                return node;

            object? value;
            try
            {
                value = Budget.Isolated(FoldOperations, () => node.Evaluate(NoVariables));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return node;
            }

            int items = FoldMaxItems, text = FoldMaxText;
            return Small(Values.Unwrap(value), ref items, ref text) ? new Literal(Frozen.Freeze(value)) : node;
        }

        // Plain values within what is left of the item and text allowances.
        private static bool Small(object? value, ref int items, ref int text)
        {
            switch (value)
            {
                case null or bool or double:
                    return true;
                case string s:
                    return (text -= s.Length) >= 0;
                case IDictionary<string, object?> dict:
                    foreach (var (key, item) in dict)
                        if (--items < 0 || (text -= key.Length) < 0 || !Small(Values.Unwrap(item), ref items, ref text))
                            return false;
                    return true;
                case System.Collections.IList list:
                    foreach (var item in list)
                        if (--items < 0 || !Small(Values.Unwrap(item), ref items, ref text))
                            return false;
                    return true;
                default:
                    return false; // a lambda, or anything else not plain
            }
        }

        // A literal of constants is built once, here, as a frozen value that
        // Set copies before writing into it; others build anew each time.
        private static Node FoldList(Node[] items)
            => items.All(i => i is Literal)
                ? new Literal(new FrozenList(items.Select(i => ((Literal)i).Value)))
                : new ListLiteral(items);

        private static Node FoldObject((string Key, Node Value)[] members)
            => members.All(m => m.Value is Literal)
                ? new Literal(new FrozenObject(members.Select(m => KeyValuePair.Create(m.Key, ((Literal)m.Value).Value))))
                : new ObjectLiteral(members);

        // a, b, c then the closing token (just consumed opening one); a trailing comma is fine.
        private List<T> Items<T>(Tk close, string what, Func<T> item)
        {
            var items = new List<T>();
            while (Current.Kind != close)
            {
                items.Add(item());
                if (Current.Kind != Tk.Comma)
                    break;
                Advance();
            }
            Expect(close, what);
            return items;
        }

        // name: value, 'any key': value
        private (string, Node) Member()
        {
            var key = Current.Kind is Tk.Identifier or Tk.String or Tk.And or Tk.Or or Tk.Not
                ? Advance()
                : throw new ScriptSyntaxException(
                    Current.Kind == Tk.End ? "Expected a key before the end" : $"Expected a key, found '{Current.Text}'",
                    Current.Position);
            Expect(Tk.Colon, "':'");
            return (key.Text, Expression(0));
        }

        // After '(': ')' or name (, name)* ')' and then '=>'.
        private bool IsLambdaAhead()
        {
            int i = _pos;
            if (_tokens[i].Kind != Tk.RParen)
            {
                while (true)
                {
                    if (_tokens[i].Kind != Tk.Identifier)
                        return false;
                    i++;
                    if (_tokens[i].Kind == Tk.RParen)
                        break;
                    if (_tokens[i].Kind != Tk.Comma)
                        return false;
                    i++;
                }
            }
            return _tokens[i + 1].Kind == Tk.Arrow;
        }

        private List<string> Parameters()
        {
            var names = Items(Tk.RParen, "')'", () => Parameter(Expect(Tk.Identifier, "a parameter name")));
            if (names.Distinct(StringComparer.Ordinal).Count() != names.Count)
                throw new ScriptSyntaxException("Parameter names must differ", Current.Position);
            return names;
        }

        private static string Parameter(Lexeme name)
            => name.Text.ToLowerInvariant() is "true" or "false" or "null"
                ? throw new ScriptSyntaxException($"'{name.Text}' cannot be a parameter name", name.Position)
                : name.Text;

        private Node Lambda(List<string> parameters)
        {
            Expect(Tk.Arrow, "'=>'");
            int before = _lambdas;
            var body = TieredNode.Wrap(Expression(0));
            bool reusable = _lambdas == before;
            _lambdas++;
            return new Lambda([.. parameters], body, reusable);
        }

        // .name and [index] after a value that is not a variable: find(xs, f).name, split(s, ',')[0]
        private Node Postfix(Node target)
        {
            var segments = Segments();
            return segments.Count == 0 ? target : Fold(new Access(target, [.. segments]));
        }

        private PathNode Path(Lexeme root) => new(root.Text, [.. Segments()]);

        private List<PathSegment> Segments()
        {
            var segments = new List<PathSegment>();
            while (true)
            {
                if (Current.Kind == Tk.Dot)
                {
                    Advance();
                    // Keywords are fine as member names: input.not, item.and
                    var member = Current.Kind is Tk.Identifier or Tk.And or Tk.Or or Tk.Not
                        ? Advance()
                        : throw new ScriptSyntaxException("Expected a name after '.'", Current.Position);
                    segments.Add(new MemberSegment(member.Text));
                }
                else if (Current.Kind == Tk.LBracket)
                {
                    Advance();
                    segments.Add(IndexOrSlice());
                    Expect(Tk.RBracket, "']'");
                }
                else
                {
                    return segments;
                }
            }
        }

        // [i] or [start:end:step] with any part left out: [1:], [:-1], [::2]
        private PathSegment IndexOrSlice()
        {
            Node? Part() => Current.Kind is Tk.Colon or Tk.RBracket ? null : Expression(0);

            var start = Part();
            if (Current.Kind != Tk.Colon)
                return new IndexSegment(start ?? throw new ScriptSyntaxException("Expected an index", Current.Position));

            Advance();
            var end = Part();
            Node? step = null;
            if (Current.Kind == Tk.Colon)
            {
                Advance();
                step = Part();
            }
            return new SliceSegment(start, end, step);
        }

        private static (BinaryOp, int)? Infix(Tk kind) => kind switch
        {
            Tk.Or => (BinaryOp.Or, 10),
            Tk.And => (BinaryOp.And, 20),
            Tk.Equal => (BinaryOp.Equal, 30),
            Tk.NotEqual => (BinaryOp.NotEqual, 30),
            Tk.Less => (BinaryOp.Less, 40),
            Tk.Greater => (BinaryOp.Greater, 40),
            Tk.LessEq => (BinaryOp.LessEq, 40),
            Tk.GreaterEq => (BinaryOp.GreaterEq, 40),
            Tk.Plus => (BinaryOp.Add, 50),
            Tk.Minus => (BinaryOp.Subtract, 50),
            Tk.Star => (BinaryOp.Multiply, 60),
            Tk.Slash => (BinaryOp.Divide, 60),
            Tk.Percent => (BinaryOp.Modulo, 60),
            _ => null
        };
    }
}
