using System.Globalization;
using System.Text;
using EchoPhase.Scripting.Tokens;
using EchoPhase.Scripting.Lexers;
using EchoPhase.Profilers;

namespace EchoPhase.Scripting.Parsers
{
    public class TemplateParser : IParser<TemplateToken>
    {
        private IList<TemplateToken> _tokens = new List<TemplateToken>();
        private IDictionary<string, object> _variables = new Dictionary<string, object>();
        private readonly StringBuilder _sb;

        private readonly ILexer<Token> _lexer;
        private readonly IParser<Token> _parser;
        private readonly IProfiler _profiler;
        private readonly ILexer<PathToken> _pathLexer;
        private readonly IPathParser<PathToken> _pathParser;

        public TemplateParser(
            ILexer<Token> lexer,
            IParser<Token> parser,
            ILexer<PathToken> pathLexer,
            IPathParser<PathToken> pathParser,
            IProfiler profiler)
        {
            _sb = new();
            _lexer = lexer;
            _parser = parser;
            _profiler = profiler;
            _pathLexer = pathLexer;
            _pathParser = pathParser;
        }

        public void With(ILexer<TemplateToken> lexer, IDictionary<string, object> variables)
        {
            _tokens = lexer.Tokens;
            _variables = variables;
        }

        public T Parse<T>()
        {
            using (_profiler.Step())
            {
                _sb.Clear();

                foreach (var token in _tokens)
                {
                    switch (token.Type)
                    {
                        case TemplateTokenType.Text:
                        case TemplateTokenType.EscapedAt:
                        case TemplateTokenType.LiteralAt:
                            _sb.Append(token.Value);
                            break;

                        case TemplateTokenType.Variable:
                            var val = Eval.Resolve(_pathLexer, _pathParser, _variables, token.Value);
                            _sb.Append(val?.ToString() ?? "");
                            break;

                        case TemplateTokenType.Expression:
                            var result = Eval.TypedExecute<string>(_lexer, _parser, token.Value, _variables);
                            _sb.Append(result);
                            break;
                    }
                }

                return (T)Convert.ChangeType(_sb.ToString(), typeof(T), CultureInfo.InvariantCulture);
            }
        }
    }
}
