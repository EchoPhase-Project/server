// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Text.Json;
using EchoPhase.Profilers;
using EchoPhase.Scripting.Extensions;
using EchoPhase.Scripting.Lexers;
using EchoPhase.Scripting.Tokens;

namespace EchoPhase.Scripting.Parsers
{
    public class Parser : IParser<Token>
    {
        private int _pos = 0;
        private IList<Token> _tokens = new List<Token>();
        private IDictionary<string, object> _variables = new Dictionary<string, object>();

        private readonly IProfiler _profiler;
        private readonly ILexer<PathToken> _pathLexer;
        private readonly IPathParser<PathToken> _pathParser;

        public Parser(
            ILexer<PathToken> pathLexer,
            IPathParser<PathToken> pathParser,
            IProfiler profiler)
        {
            _profiler = profiler;
            _pathLexer = pathLexer;
            _pathParser = pathParser;
        }

        public void With(ILexer<Token> lexer, IDictionary<string, object> variables)
        {
            _pos = 0;
            _tokens = lexer.Tokens;
            _variables = variables;
        }

        private Token current =>
            _pos < _tokens.Count
                ? _tokens[_pos]
                : throw new InvalidOperationException($"Unexpected end of input: {_tokens[^1]} {_pos}");

        private Token Advance()
        {
            if (_pos >= _tokens.Count)
                throw new InvalidOperationException($"Unexpected end of input: {_tokens[^1]} {_pos}");
            return _tokens[_pos++];
        }

        public T Parse<T>() =>
            (T)Convert.ChangeType(ParseExpression(), typeof(T), CultureInfo.InvariantCulture);

        private object ParseExpression(int minBindingPower = 0)
        {
            using (_profiler.Step())
            {
                var token = Advance();
                var left = Nud(token);

                while (true)
                {
                    var next = current;
                    if (!BindingPower(next.Type, out var lbp, out var rbp) || lbp < minBindingPower)
                        break;

                    Advance();

                    if (next.Type == TokenType.Question)
                    {
                        var trueExpr = ParseExpression();
                        if (current.Type != TokenType.Colon)
                            throw new InvalidOperationException("Expected ':' in ternary expression");
                        Advance();
                        var falseExpr = ParseExpression(rbp);
                        left = ToBool(left) ? trueExpr : falseExpr;
                    }
                    else
                    {
                        var right = ParseExpression(rbp);
                        left = EvalBinary(token: next, left, right);
                    }
                }

                return left;
            }
        }

        private object Nud(Token token)
        {
            using (_profiler.Step())
            {
                return token.Type switch
                {
                    TokenType.Number => token.Number,
                    TokenType.String => token.Text.ToString(),
                    TokenType.Identifier => HandleIdentifier(token),
                    TokenType.Minus => -ToNumber(ParseExpression(70)),
                    TokenType.Not => ToBool(ParseExpression(70)) ? 0 : 1,
                    TokenType.LParen =>
                        ParseExpression().Also(_ =>
                        {
                            if (current.Type != TokenType.RParen)
                                throw new InvalidOperationException("Expected closing parenthesis");
                            Advance();
                        }),
                    _ => throw new NotSupportedException($"Unexpected token: {token.Type}")
                };
            }
        }

        private object EvalBinary(Token token, object left, object right)
        {
            return token.Type switch
            {
                TokenType.Plus => ToNumber(left) + ToNumber(right),
                TokenType.Minus => ToNumber(left) - ToNumber(right),
                TokenType.Star => ToNumber(left) * ToNumber(right),
                TokenType.Slash => ToNumber(left) / ToNumber(right),
                TokenType.Mod => ToNumber(left) % ToNumber(right),
                TokenType.Equal => CompareOperands(left, right, "==") ? 1 : 0,
                TokenType.NotEqual => CompareOperands(left, right, "!=") ? 1 : 0,
                TokenType.Less => CompareOperands(left, right, "<") ? 1 : 0,
                TokenType.Greater => CompareOperands(left, right, ">") ? 1 : 0,
                TokenType.LessEq => CompareOperands(left, right, "<=") ? 1 : 0,
                TokenType.GreaterEq => CompareOperands(left, right, ">=") ? 1 : 0,
                TokenType.And => ToBool(left) && ToBool(right) ? 1 : 0,
                TokenType.Or => ToBool(left) || ToBool(right) ? 1 : 0,
                _ => throw new NotSupportedException($"Unhandled binary operator: {token.Type}")
            };
        }

        private static bool BindingPower(TokenType type, out int lbp, out int rbp)
        {
            (lbp, rbp) = type switch
            {
                TokenType.Or => (10, 11),
                TokenType.And => (20, 21),
                TokenType.Equal or TokenType.NotEqual => (30, 31),
                TokenType.Less or TokenType.Greater or TokenType.LessEq or TokenType.GreaterEq => (40, 41),
                TokenType.Plus or TokenType.Minus => (50, 51),
                TokenType.Star or TokenType.Slash or TokenType.Mod => (60, 61),
                TokenType.Question => (5, 4),
                _ => (0, 0)
            };
            return lbp != 0;
        }

        private object ParseFunctionCall(string name)
        {
            using (_profiler.Step())
            {
                Advance();
                var args = new List<object>();
                if (current.Type != TokenType.RParen)
                {
                    do
                    {
                        args.Add(ParseExpression());
                    } while (current.Type == TokenType.Comma && Advance() != null);
                }
                if (current.Type != TokenType.RParen)
                    throw new InvalidOperationException("Expected ')' after function arguments");
                Advance();

                return name switch
                {
                    "min" => Math.Min(ToNumber(args[0]), ToNumber(args[1])),
                    "max" => Math.Max(ToNumber(args[0]), ToNumber(args[1])),
                    "abs" => Math.Abs(ToNumber(args[0])),
                    "sqrt" => Math.Sqrt(ToNumber(args[0])),
                    "round" => Math.Round(ToNumber(args[0])),
                    _ => throw new NotSupportedException($"Unknown function: {name}")
                };
            }
        }

        private object HandleIdentifier(Token token)
        {
            using (_profiler.Step())
            {
                var name = token.Text.ToString().ToLowerInvariant();

                if (current.Type == TokenType.LParen)
                    return ParseFunctionCall(name);

                return name switch
                {
                    "true" => 1,
                    "false" => 0,
                    "null" => "",
                    "not" => ToBool(ParseExpression(70)) ? 0 : 1,
                    _ => Eval.Resolve(_pathLexer, _pathParser, _variables, token.Text.ToString()) ?? ""
                };
            }
        }

        private double ToNumber(object? value)
        {
            if (value == null)
                throw new ArgumentNullException("Value is null.");

            if (value is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            if (value is bool b) return b ? 1 : 0;

            if (value is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.True) return 1;
                if (je.ValueKind == JsonValueKind.False) return 0;
                return je.TryGetDouble(out var d) ? d : 0;
            }

            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private bool ToBool(object? value)
        {
            if (value == null) return false;
            if (value is string s) return !string.IsNullOrEmpty(s);
            if (value is bool b) return b;
            if (value is double d) return Math.Abs(d) > double.Epsilon;

            if (value is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.True) return true;
                if (je.ValueKind == JsonValueKind.False) return false;
                return je.TryGetDouble(out var jd) && Math.Abs(jd) > double.Epsilon;
            }

            try { return Math.Abs(Convert.ToDouble(value, CultureInfo.InvariantCulture)) > double.Epsilon; }
            catch { return false; }
        }

        private object UnwrapValue(object obj)
        {
            if (obj is JsonElement je)
            {
                return je.ValueKind switch
                {
                    JsonValueKind.String => je.GetString()!,
                    JsonValueKind.Number => je.TryGetInt64(out long l) ? (object)l : je.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => je.GetRawText()
                };
            }
            return obj;
        }

        private static readonly Dictionary<Type, int> typePriority = new()
        {
            { typeof(byte), 1 },
            { typeof(sbyte), 1 },
            { typeof(short), 2 },
            { typeof(ushort), 2 },
            { typeof(int), 3 },
            { typeof(uint), 3 },
            { typeof(long), 4 },
            { typeof(ulong), 4 },
            { typeof(float), 5 },
            { typeof(double), 6 },
            { typeof(decimal), 7 },
            { typeof(string), 8 },
        };

        private bool CompareOperands(object left, object right, string op)
        {
            if (left == null || right == null)
                throw new ArgumentNullException("Operands must not be null.");

            left = UnwrapValue(left);
            right = UnwrapValue(right);

            Type leftType = left.GetType();
            Type rightType = right.GetType();

            if (leftType == rightType)
            {
                return CompareDirect(left, right, op);
            }

            if (!typePriority.TryGetValue(leftType, out int leftPriority) ||
                !typePriority.TryGetValue(rightType, out int rightPriority))
            {
                throw new InvalidOperationException($"Operands have incompatible or unsupported types: {leftType} and {rightType}.");
            }

            if (leftPriority < rightPriority)
            {
                left = Convert.ChangeType(left, rightType);
            }
            else
            {
                right = Convert.ChangeType(right, leftType);
            }

            return CompareDirect(left, right, op);
        }

        private bool CompareDirect(object left, object right, string op)
        {
            int comparisonResult;
            if (left is string ls && right is string rs)
            {
                comparisonResult = string.Compare(ls, rs, StringComparison.Ordinal);
            }
            else if (left is IComparable comp)
            {
                comparisonResult = comp.CompareTo(right);
            }
            else
            {
                throw new InvalidOperationException($"Type {left.GetType()} is not comparable.");
            }

            return op switch
            {
                "==" => comparisonResult == 0,
                "!=" => comparisonResult != 0,
                ">" => comparisonResult > 0,
                "<" => comparisonResult < 0,
                ">=" => comparisonResult >= 0,
                "<=" => comparisonResult <= 0,
                _ => throw new NotSupportedException($"Unknown operator {op}")
            };
        }
    }
}
