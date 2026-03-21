// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using EchoPhase.Profilers;
using EchoPhase.Scripting.Tokens;

namespace EchoPhase.Scripting.Lexers
{
    public class TemplateLexer : ILexer<TemplateToken>
    {
        private int _pos = 0;
        private string _input = string.Empty;

        public IList<TemplateToken> Tokens { get; } = new List<TemplateToken>();

        private readonly StringBuilder _sb;
        private readonly IProfiler _profiler;

        public TemplateLexer(
            IProfiler profiler)
        {
            _sb = new();
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
                while (_pos < _input.Length)
                {
                    if (Match("@@"))
                    {
                        EmitText(_sb);
                        Tokens.Add(new TemplateToken(TemplateTokenType.EscapedAt, "@"));
                        _pos += 2;
                    }
                    else if (Match("\\@"))
                    {
                        EmitText(_sb);
                        Tokens.Add(new TemplateToken(TemplateTokenType.LiteralAt, "@"));
                        _pos += 2;
                    }
                    else if (Match("@("))
                    {
                        EmitText(_sb);
                        var expr = ReadBalanced();
                        Tokens.Add(new TemplateToken(TemplateTokenType.Expression, expr));
                    }
                    else if (Match("@"))
                    {
                        EmitText(_sb);
                        var id = ReadIdentifier();
                        Tokens.Add(new TemplateToken(TemplateTokenType.Variable, id));
                    }
                    else
                    {
                        _sb.Append(_input[_pos++]);
                    }
                }
                EmitText(_sb);
            }
        }

        private void EmitText(StringBuilder sb)
        {
            if (sb.Length > 0)
            {
                Tokens.Add(new TemplateToken(TemplateTokenType.Text, sb.ToString()));
                sb.Clear();
            }
        }

        private bool Match(string s) =>
            _pos + s.Length <= _input.Length && _input.Substring(_pos, s.Length) == s;

        private string ReadBalanced()
        {
            using (_profiler.Step())
            {
                _pos += 2;
                int depth = 1;
                int start = _pos;

                while (_pos < _input.Length)
                {
                    if (_input[_pos] == '(') depth++;
                    else if (_input[_pos] == ')') depth--;
                    if (depth == 0) break;
                    _pos++;
                }

                if (depth != 0) throw new InvalidOperationException("Unmatched ( in expression");

                var content = _input.Substring(start, _pos - start);
                _pos++;
                return content;
            }
        }

        private string ReadIdentifier()
        {
            using (_profiler.Step())
            {
                int start = ++_pos;
                while (_pos < _input.Length &&
                       (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_' || _input[_pos] == '.' || _input[_pos] == '[' || _input[_pos] == ']'))
                    _pos++;
                return _input.Substring(start, _pos - start);
            }
        }
    }
}
