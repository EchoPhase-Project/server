using System.Collections;
using EchoPhase.Scripting.Tokens;
using EchoPhase.Scripting.Lexers;
using EchoPhase.Profilers;
using Newtonsoft.Json.Linq;
using EchoPhase.Types.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Scripting.Parsers
{
    public class PathParser : IPathParser<PathToken>
    {
        private int _pos = 0;
        private IList<PathToken> _tokens = new List<PathToken>();
        private IDictionary<string, object> _variables = new Dictionary<string, object>();

        private readonly IProfiler _profiler;
        private readonly IServiceProvider _serviceProvider;

        public PathParser(IServiceProvider serviceProvider, IProfiler profiler)
        {
            _serviceProvider = serviceProvider;
            _profiler = profiler;
        }

        public void With(ILexer<PathToken> lexer, IDictionary<string, object> variables)
        {
            _pos = 0;
            _tokens = lexer.Tokens;
            _variables = variables;
        }

        public object? Resolve()
        {
            if (_tokens.Count == 0)
                return null;

            object? current = null;

            if (_tokens[_pos].Type != PathTokenType.Identifier)
                throw new InvalidOperationException("Path must start with variable name.");

            string varName = _tokens[_pos++].Value;
            if (!_variables.TryGetValue(varName, out current))
                throw new KeyNotFoundException($"Variable '{varName}' not found.");

            while (_pos < _tokens.Count)
            {
                var token = _tokens[_pos++];

                if (token.Type == PathTokenType.Dot)
                {
                    var next = _tokens[_pos++];
                    if (next.Type != PathTokenType.Identifier)
                        throw new InvalidOperationException("Expected identifier after '.'");

                    string propName = next.Value;

                    if (current is JObject jObj)
                        current = jObj[propName];
                    else if (current is JToken jTok)
                        current = jTok[propName];
                    else
                    {
                        var prop = current?.GetType().GetProperty(propName);
                        if (prop == null)
                            throw new KeyNotFoundException($"Property '{propName}' not found.");
                        current = prop.GetValue(current);
                    }
                }
                else if (token.Type == PathTokenType.LBracket)
                {
                    if (_pos >= _tokens.Count || _tokens[_pos].Type != PathTokenType.Expression)
                        throw new InvalidOperationException("Expected expression inside brackets.");

                    string innerExprText = _tokens[_pos++].Value;

                    if (_pos >= _tokens.Count || _tokens[_pos].Type != PathTokenType.RBracket)
                        throw new InvalidOperationException("Missing closing ']' in path.");
                    _pos++;

                    int idx;
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var lexerForExpr = scope.ServiceProvider.GetRequiredService<ILexer<Token>>();
                        var parserForExpr = scope.ServiceProvider.GetRequiredService<IParser<Token>>();

                        var evalResult = Eval.Execute<int>(lexerForExpr, parserForExpr, innerExprText, _variables);
                        if (!evalResult.TryGetValue(out idx))
                            throw new InvalidOperationException($"Index expression '{innerExprText}' did not evaluate to int.");
                    }

                    if (current is JArray jArr)
                    {
                        if (idx < 0 || idx >= jArr.Count)
                            throw new IndexOutOfRangeException($"Index {idx} out of range for JArray (length={jArr.Count}).");
                        current = jArr[idx];
                    }
                    else if (current is IList list)
                    {
                        if (idx < 0 || idx >= list.Count)
                            throw new IndexOutOfRangeException($"Index {idx} out of range for IList (count={list.Count}).");
                        current = list[idx];
                    }
                    else
                    {
                        throw new InvalidOperationException("Value is not indexable");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected token: {token.Type}");
                }
            }

            if (current is JValue jVal && jVal.Type == JTokenType.Null)
                return null;

            return current;
        }

        public IDictionary<string, object> Set(object? value)
        {
            int _pos = 0;
            object? current;

            if (_tokens[_pos].Type != PathTokenType.Identifier)
                throw new InvalidOperationException("Path must start with variable name.");

            string varName = _tokens[_pos++].Value;
            if (!_variables.TryGetValue(varName, out current) || current == null)
            {
                current = _tokens.Any(t => t.Type == PathTokenType.LBracket) ? new JArray() : new JObject();
                _variables[varName] = current;
            }

            object? parent = null;
            PathToken? lastToken = null;
            int? lastIndex = null;

            while (_pos < _tokens.Count)
            {
                var token = _tokens[_pos++];

                if (token.Type == PathTokenType.Dot)
                {
                    var next = _tokens[_pos++];
                    if (next.Type != PathTokenType.Identifier)
                        throw new InvalidOperationException("Expected identifier after '.'");

                    string propName = next.Value;
                    parent = current;
                    lastToken = next;

                    if (current is JObject jObj)
                    {
                        if (!jObj.ContainsKey(propName))
                            jObj[propName] = new JObject();
                        current = jObj[propName];
                    }
                    else if (current is IDictionary<string, object> dict)
                    {
                        if (!dict.ContainsKey(propName))
                            dict[propName] = new Dictionary<string, object>();
                        current = dict[propName];
                    }
                    else
                    {
                        var prop = current?.GetType().GetProperty(propName);
                        if (prop == null)
                            throw new KeyNotFoundException($"Property '{propName}' not found.");
                        current = prop.GetValue(current);
                    }
                }
                else if (token.Type == PathTokenType.LBracket)
                {
                    if (_pos >= _tokens.Count || _tokens[_pos].Type != PathTokenType.Expression)
                        throw new InvalidOperationException("Expected expression inside brackets.");

                    string innerExprText = _tokens[_pos++].Value;

                    if (_pos >= _tokens.Count || _tokens[_pos].Type != PathTokenType.RBracket)
                        throw new InvalidOperationException("Missing closing ']' in path.");
                    _pos++;

                    int idx;
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var lexerForExpr = scope.ServiceProvider.GetRequiredService<ILexer<Token>>();
                        var parserForExpr = scope.ServiceProvider.GetRequiredService<IParser<Token>>();

                        var evalResult = Eval.Execute<int>(lexerForExpr, parserForExpr, innerExprText, _variables);
                        if (!evalResult.TryGetValue(out idx))
                            throw new InvalidOperationException($"Index expression '{innerExprText}' did not evaluate to int.");
                    }

                    if (current is JArray jArr)
                    {
                        while (jArr.Count <= idx)
                            jArr.Add(JValue.CreateNull());
                        current = jArr[idx];
                    }
                    else if (current is IList list)
                    {
                        while (list.Count <= idx)
                            list.Add(null!);
                        current = list[idx];
                    }
                    else
                        throw new InvalidOperationException("Value is not indexable");
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected token: {token.Type}");
                }
            }

            if (parent != null && lastToken != null)
            {
                if (lastToken.Type == PathTokenType.Identifier)
                {
                    if (parent is JObject jObj)
                        jObj[lastToken.Value] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
                    else if (parent is IDictionary<string, object> dict)
                        dict[lastToken.Value] = value ?? JValue.CreateNull();
                }
                else if (lastToken.Type == PathTokenType.LBracket && lastIndex.HasValue)
                {
                    if (parent is JArray jArr)
                    {
                        Console.WriteLine($"[DEBUG] JArray assignment at index {lastIndex.Value}: Current Value={jArr[lastIndex.Value]}, New Value={value}");
                        jArr[lastIndex.Value] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
                    }
                    else if (parent is IList list)
                    {
                        Console.WriteLine($"[DEBUG] IList assignment at index {lastIndex.Value}: Current Value={list[lastIndex.Value]}, New Value={value}, Current Type={list[lastIndex.Value]?.GetType().Name ?? "null"}, Value Type={value?.GetType().Name ?? "null"}");
                        list[lastIndex.Value] = value;
                    }
                }
            }
            else
            {
                _variables[varName] = value == null ? JValue.CreateNull() : value;
            }

            return _variables;
        }
    }
}
