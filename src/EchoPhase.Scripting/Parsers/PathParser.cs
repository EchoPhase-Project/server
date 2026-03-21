// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Text.Json;
using EchoPhase.Profilers;
using EchoPhase.Scripting.Lexers;
using EchoPhase.Scripting.Tokens;
using EchoPhase.Types.Result.Extensions;
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

            if (_tokens[_pos].Type != PathTokenType.Identifier)
                throw new InvalidOperationException("Path must start with variable name.");

            string varName = _tokens[_pos++].Value;
            if (!_variables.TryGetValue(varName, out var current))
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

                    if (current is IDictionary<string, object?> dict)
                        current = dict.TryGetValue(propName, out var v) ? v : null;
                    else if (current is JsonElement jsonElem && jsonElem.ValueKind == JsonValueKind.Object)
                        current = jsonElem.TryGetProperty(propName, out var prop) ? GetJsonSimpleValue(prop) : null;
                    else
                    {
                        var prop = (current?.GetType().GetProperty(propName)) ?? throw new KeyNotFoundException($"Property '{propName}' not found.");
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

                    if (current is IList<object?> list)
                    {
                        if (idx < 0 || idx >= list.Count)
                            throw new InvalidOperationException($"Index {idx} out of range (length={list.Count}).");
                        current = list[idx];
                    }
                    else if (current is IList legacyList)
                    {
                        if (idx < 0 || idx >= legacyList.Count)
                            throw new InvalidOperationException($"Index {idx} out of range (count={legacyList.Count}).");
                        current = legacyList[idx];
                    }
                    else
                        throw new InvalidOperationException("Value is not indexable");
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected token: {token.Type}");
                }
            }

            return current;
        }

        public IDictionary<string, object> Set(object? value)
        {
            int pos = 0;

            if (_tokens[pos].Type != PathTokenType.Identifier)
                throw new InvalidOperationException("Path must start with variable name.");

            string varName = _tokens[pos++].Value;
            if (!_variables.TryGetValue(varName, out var current) || current == null)
            {
                current = _tokens.Any(t => t.Type == PathTokenType.LBracket)
                    ? new List<object?>()
                    : new Dictionary<string, object?>();
                _variables[varName] = current;
            }

            object? parent = null;
            string? lastKey = null;
            int? lastIndex = null;

            while (pos < _tokens.Count)
            {
                var token = _tokens[pos++];

                if (token.Type == PathTokenType.Dot)
                {
                    var next = _tokens[pos++];
                    if (next.Type != PathTokenType.Identifier)
                        throw new InvalidOperationException("Expected identifier after '.'");

                    string propName = next.Value;
                    parent = current;
                    lastKey = propName;
                    lastIndex = null;

                    if (current is IDictionary<string, object?> dict)
                    {
                        if (!dict.ContainsKey(propName))
                            dict[propName] = new Dictionary<string, object?>();
                        current = dict[propName];
                    }
                    else
                    {
                        var prop = (current?.GetType().GetProperty(propName)) ?? throw new KeyNotFoundException($"Property '{propName}' not found.");
                        current = prop.GetValue(current);
                    }
                }
                else if (token.Type == PathTokenType.LBracket)
                {
                    if (_pos >= _tokens.Count || _tokens[pos].Type != PathTokenType.Expression)
                        throw new InvalidOperationException("Expected expression inside brackets.");

                    string innerExprText = _tokens[pos++].Value;

                    if (pos >= _tokens.Count || _tokens[pos].Type != PathTokenType.RBracket)
                        throw new InvalidOperationException("Missing closing ']' in path.");
                    pos++;

                    int idx;
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var lexerForExpr = scope.ServiceProvider.GetRequiredService<ILexer<Token>>();
                        var parserForExpr = scope.ServiceProvider.GetRequiredService<IParser<Token>>();
                        var evalResult = Eval.Execute<int>(lexerForExpr, parserForExpr, innerExprText, _variables);
                        if (!evalResult.TryGetValue(out idx))
                            throw new InvalidOperationException($"Index expression '{innerExprText}' did not evaluate to int.");
                    }

                    parent = current;
                    lastIndex = idx;
                    lastKey = null;

                    if (current is IList<object?> list)
                    {
                        while (list.Count <= idx)
                            list.Add(null);
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

            if (parent != null)
            {
                if (lastKey != null && parent is IDictionary<string, object?> dict)
                    dict[lastKey] = value;
                else if (lastIndex.HasValue && parent is IList<object?> list)
                    list[lastIndex.Value] = value;
            }
            else
            {
                _variables[varName] = value!;
            }

            return _variables;
        }

        private static object? GetJsonSimpleValue(JsonElement elem) => elem.ValueKind switch
        {
            JsonValueKind.String => elem.GetString(),
            JsonValueKind.Number => elem.TryGetInt64(out long l) ? l : elem.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => elem.GetRawText()
        };
    }
}
