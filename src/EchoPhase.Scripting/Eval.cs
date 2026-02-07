using System.Linq.Expressions;
using EchoPhase.Scripting.Lexers;
using EchoPhase.Scripting.Parsers;
using EchoPhase.Scripting.Tokens;
using EchoPhase.Types.Results;

namespace EchoPhase.Scripting
{
    public static class Eval
    {
        public static IServiceResult<T> Process<T>(ILexer<TemplateToken> lexer, IParser<TemplateToken> parser, string input, IDictionary<string, object> variables)
        {
            try
            {
                var result = TypedProcess<T>(lexer, parser, input, variables);
                return ServiceResult<T>.Success(result);
            }
            catch (Exception ex)
            {
                return ServiceResult<T>.Failure(err =>
                    err.Set(ex.GetType().Name, ex.Message));
            }
        }

        public static T TypedProcess<T>(ILexer<TemplateToken> lexer, IParser<TemplateToken> parser, string input, IDictionary<string, object> variables)
        {
            lexer.With(input);
            parser.With(lexer, variables);
            var result = parser.Parse<T>();
            return result;
        }

        public static IServiceResult<T> Execute<T>(ILexer<Token> lexer, IParser<Token> parser, string input, IDictionary<string, object> variables)
        {
            try
            {
                var result = TypedExecute<T>(lexer, parser, input, variables);
                return ServiceResult<T>.Success(result);
            }
            catch (Exception ex)
            {
                return ServiceResult<T>.Failure(err =>
                    err.Set(ex.GetType().Name, ex.Message));
            }
        }

        public static T TypedExecute<T>(ILexer<Token> lexer, IParser<Token> parser, string input, IDictionary<string, object> variables)
        {
            lexer.With(input);
            parser.With(lexer, variables);
            var result = parser.Parse<T>();
            return result;
        }

        public static IServiceResult<TR> ProcessTemplate<TM, TR>(ILexer<TemplateToken> lexer, IParser<TemplateToken> parser, string input, TM model)
        {
            var variables = ToVariableDictionary(model);
            return Process<TR>(lexer, parser, input, variables);
        }

        public static object? Resolve(ILexer<PathToken> lexer, IPathParser<PathToken> parser, IDictionary<string, object> variables, string path)
        {
            lexer.With(path);
            parser.With(lexer, variables);
            return parser.Resolve();
        }

        public static IDictionary<string, object> Set(ILexer<PathToken> lexer, IPathParser<PathToken> parser, IDictionary<string, object> variables, string path, object? value)
        {
            lexer.With(path);
            parser.With(lexer, variables);
            return parser.Set(value);
        }

        private static IDictionary<string, object> ToVariableDictionary<T>(T model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var dict = typeof(T)
                .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .Where(p => p.CanRead)
                .ToDictionary(p => p.Name, p => p.GetValue(model) ?? "");

            return dict;
        }
    }
}
