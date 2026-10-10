// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// Pratt parser from lexemes to a <see cref="Node"/> tree. Precedence,
    /// loosest first: ?: , ||, &&, == !=, &lt; &gt; &lt;= &gt;=, + -, * / %, unary - !.
    /// </summary>
    internal sealed class ExpressionParser
    {
        private const int UnaryPower = 70;

        private readonly List<Lexeme> _tokens;
        private int _pos;

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
                    left = new Conditional(left, whenTrue, whenFalse);
                    continue;
                }

                if (Infix(op.Kind) is not var (binary, power) || power < minPower)
                    break;

                Advance();
                left = new Binary(binary, left, Expression(power + 1));
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
                    return new Unary(UnaryOp.Negate, Expression(UnaryPower));
                case Tk.Not:
                    return new Unary(UnaryOp.Not, Expression(UnaryPower));
                case Tk.LParen:
                    var inner = Expression(0);
                    Expect(Tk.RParen, "')'");
                    return inner;
                case Tk.Identifier:
                    switch (token.Text.ToLowerInvariant())
                    {
                        case "true": return new Literal(true);
                        case "false": return new Literal(false);
                        case "null": return new Literal(null);
                    }
                    return Current.Kind == Tk.LParen ? Call(token) : Path(token);
                case Tk.End:
                    throw new ScriptSyntaxException("Unexpected end of expression", token.Position);
                default:
                    throw new ScriptSyntaxException($"Unexpected '{token.Text}'", token.Position);
            }
        }

        private Node Call(Lexeme name)
        {
            Advance(); // (
            var arguments = new List<Node>();
            if (Current.Kind != Tk.RParen)
            {
                do
                {
                    arguments.Add(Expression(0));
                }
                while (Current.Kind == Tk.Comma && Advance().Kind == Tk.Comma);
            }
            Expect(Tk.RParen, "')'");

            return new Call(name.Text, Functions.Resolve(name.Text, arguments.Count, name.Position), [.. arguments]);
        }

        private PathNode Path(Lexeme root)
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
                    segments.Add(new IndexSegment(Expression(0)));
                    Expect(Tk.RBracket, "']'");
                }
                else
                {
                    return new PathNode(root.Text, segments);
                }
            }
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
