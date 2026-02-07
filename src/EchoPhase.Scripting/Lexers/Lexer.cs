using EchoPhase.Scripting.Tokens;
using EchoPhase.Profilers;

namespace EchoPhase.Scripting.Lexers
{
    public class Lexer : ILexer<Token>
    {
        private int _pos = 0;
        private string _input = string.Empty;

        public IList<Token> Tokens
        {
            get; private set;
        }

        private readonly IProfiler _profiler;

        public Lexer(
            IProfiler profiler)
        {
            Tokens = new List<Token>();
            _profiler = profiler;
        }

        public void With(string input)
        {
            _pos = 0;
            _input = input;
            Tokens.Clear();
            Tokenize();
        }

        private void Tokenize()
        {
            using (_profiler.Step())
            {
                while (!IsAtEnd())
                {
                    SkipWhitespace();

                    if (IsAtEnd())
                        break;

                    char c = Peek();

                    if (char.IsDigit(c))
                    {
                        Tokens.Add(ReadNumber());
                    }
                    else if (char.IsLetter(c) || c == '_')
                    {
                        Tokens.Add(ReadIdentifier());
                    }
                    else if (c == '"' || c == '\'')
                    {
                        Tokens.Add(ReadString());
                    }
                    else
                    {
                        Tokens.Add(ReadOperatorOrPunct());
                    }
                }

                Tokens.Add(new Token(TokenType.End, string.Empty, 0, 0));
            }
        }

        private Token ReadNumber()
        {
            using (_profiler.Step())
            {
                int start = _pos;
                while (!IsAtEnd() && (char.IsDigit(Peek()) || Peek() == '.'))
                    Advance();

                return new Token(TokenType.Number, _input, start, _pos);
            }
        }

        private Token ReadIdentifier()
        {
            using (_profiler.Step())
            {
                int start = _pos;

                while (!IsAtEnd())
                {
                    char c = Peek();

                    if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
                    {
                        Advance();
                    }
                    else if (c == '[')
                    {
                        ReadBrackets();
                    }
                    else
                    {
                        break;
                    }
                }

                return new Token(TokenType.Identifier, _input, start, _pos);
            }
        }

        private void ReadBrackets()
        {
            int depth = 0;

            while (!IsAtEnd())
            {
                char c = Peek();

                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                    Advance();
                    if (depth == 0)
                        break;
                    continue;
                }

                Advance();
            }

            if (depth != 0)
                throw new Exception("Unterminated brackets in identifier");
        }

        private Token ReadString()
        {
            using (_profiler.Step())
            {
                char quote = Advance();
                int start = _pos;

                while (!IsAtEnd() && Peek() != quote)
                    Advance();

                if (IsAtEnd())
                    throw new Exception("Unterminated string literal");

                int end = _pos;
                Advance();

                return new Token(TokenType.String, _input, start, end);
            }
        }

        private Token ReadOperatorOrPunct()
        {
            using (_profiler.Step())
            {
                char current = Peek();
                char next = Peek(1);

                string two = $"{current}{next}";
                if (two == "==" || two == "!=" || two == "<=" || two == ">=" || two == "&&" || two == "||")
                {
                    Advance(2);
                    return new Token(two switch
                    {
                        "==" => TokenType.Equal,
                        "!=" => TokenType.NotEqual,
                        "<=" => TokenType.LessEq,
                        ">=" => TokenType.GreaterEq,
                        "&&" => TokenType.And,
                        "||" => TokenType.Or,
                        _ => throw new Exception()
                    }, two, 0, two.Length);
                }

                Advance();
                return new Token(current switch
                {
                    '+' => TokenType.Plus,
                    '-' => TokenType.Minus,
                    '*' => TokenType.Star,
                    '/' => TokenType.Slash,
                    '(' => TokenType.LParen,
                    ')' => TokenType.RParen,
                    '?' => TokenType.Question,
                    ':' => TokenType.Colon,
                    ',' => TokenType.Comma,
                    '<' => TokenType.Less,
                    '>' => TokenType.Greater,
                    '!' => TokenType.Not,
                    '%' => TokenType.Mod,
                    '=' => throw new Exception("Unexpected single '='; did you mean '=='?"),
                    _ => throw new NotSupportedException($"Unknown character: '{current}'")
                }, current.ToString(), 0, 1);
            }
        }

        private void SkipWhitespace()
        {
            while (!IsAtEnd() && char.IsWhiteSpace(Peek()))
                Advance();
        }

        private bool IsAtEnd() => _pos >= _input.Length;

        private char Peek(int offset = 0)
        {
            return _pos + offset < _input.Length ? _input[_pos + offset] : '\0';
        }

        private char Advance(int count = 1)
        {
            char c = _input[_pos];
            _pos += count;
            return c;
        }
    }
}
