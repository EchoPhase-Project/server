using System.Text;
using EchoPhase.Scripting.Tokens;
using EchoPhase.Profilers;

namespace EchoPhase.Scripting.Lexers
{
    public class PathLexer : ILexer<PathToken>
    {
        public IList<PathToken> Tokens { get; private set; } = new List<PathToken>();
        private readonly IProfiler _profiler;
        private string _input = string.Empty;

        public PathLexer(IProfiler profiler)
        {
            _profiler = profiler;
        }

        public void With(string input)
        {
            _input = input;
            Tokens.Clear();
            Tokenize();
        }

        private void Tokenize()
        {
            var sb = new StringBuilder();

            for (int i = 0; i < _input.Length; i++)
            {
                char c = _input[i];

                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    sb.Append(c);
                    continue;
                }

                if (sb.Length > 0)
                {
                    Tokens.Add(new PathToken(PathTokenType.Identifier, sb.ToString()));
                    sb.Clear();
                }

                switch (c)
                {
                    case '.':
                        Tokens.Add(new PathToken(PathTokenType.Dot, "."));
                        break;

                    case '[':
                        Tokens.Add(new PathToken(PathTokenType.LBracket, "["));

                        var exprSb = new StringBuilder();
                        int bracketDepth = 1;
                        i++;

                        while (i < _input.Length && bracketDepth > 0)
                        {
                            char ch = _input[i];

                            if (ch == '[')
                            {
                                bracketDepth++;
                                exprSb.Append(ch);
                            }
                            else if (ch == ']')
                            {
                                bracketDepth--;
                                if (bracketDepth > 0)
                                    exprSb.Append(ch);
                            }
                            else
                            {
                                exprSb.Append(ch);
                            }

                            i++;
                        }

                        if (bracketDepth != 0)
                            throw new InvalidOperationException("Unterminated brackets in path expression.");

                        Tokens.Add(new PathToken(PathTokenType.Expression, exprSb.ToString()));
                        Tokens.Add(new PathToken(PathTokenType.RBracket, "]"));
                        break;

                    case ']':
                        throw new InvalidOperationException("Unexpected closing bracket.");

                    default:
                        throw new InvalidOperationException($"Unexpected character: {c}");
                }
            }

            if (sb.Length > 0)
                Tokens.Add(new PathToken(PathTokenType.Identifier, sb.ToString()));
        }
    }
}
