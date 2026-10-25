// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text;

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// A parsed template: text with <c>@path</c> and <c>@(expression)</c>
    /// holes. <c>@@</c> and <c>\@</c> are a literal @, as is an @ that starts
    /// neither (an e-mail address). A path ends before a dot that no name
    /// follows, so "Hi @name." keeps its full stop.
    /// </summary>
    internal sealed class Template
    {
        private readonly object[] _parts; // string or Node

        private Template(object[] parts)
        {
            _parts = parts;
        }

        /// <summary>The template is one hole and nothing else: its value keeps its type.</summary>
        public bool IsSingleValue => _parts is [Node];

        /// <summary>No holes: plain text.</summary>
        public bool IsText => _parts.All(p => p is string);

        public string Render(IDictionary<string, object?> variables)
        {
            if (_parts is [string only])
                return only;

            var sb = new StringBuilder();
            foreach (var part in _parts)
                sb.Append(part is Node node ? Values.Format(node.Evaluate(variables)) : (string)part);
            return sb.ToString();
        }

        /// <summary>A single hole's value as is (a list stays a list); otherwise the rendered text.</summary>
        public object? Evaluate(IDictionary<string, object?> variables)
            => _parts is [Node node] ? node.Evaluate(variables) : Render(variables);

        public static Template Parse(string source)
        {
            var parts = new List<object>();
            var text = new StringBuilder();
            int i = 0;

            void Flush()
            {
                if (text.Length > 0)
                {
                    parts.Add(text.ToString());
                    text.Clear();
                }
            }

            while (i < source.Length)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                if ((c == '@' && next == '@') || (c == '\\' && next == '@'))
                {
                    text.Append('@');
                    i += 2;
                }
                else if (c == '@' && next == '(')
                {
                    int start = i + 2;
                    int end = MatchingParen(source, start, i);
                    Flush();
                    parts.Add(TieredNode.Wrap(Compile(() => ExpressionParser.Parse(source[start..end]), start)));
                    i = end + 1;
                }
                else if (c == '@' && (char.IsLetter(next) || next == '_'))
                {
                    int start = i + 1;
                    int end = PathEnd(source, start);
                    Flush();
                    parts.Add(Compile(() => ExpressionParser.ParsePath(source[start..end]), start));
                    i = end;
                }
                else
                {
                    text.Append(c);
                    i++;
                }
            }

            Flush();
            return new Template([.. parts]);
        }

        // Errors point into the template, not into the hole.
        private static Node Compile(Func<Node> parse, int offset)
        {
            try
            {
                return parse();
            }
            catch (ScriptSyntaxException ex)
            {
                throw new ScriptSyntaxException(ex.Message[..ex.Message.LastIndexOf(" (at ", StringComparison.Ordinal)], ex.Position + offset);
            }
        }

        // The ')' closing the '(' before start, skipping parentheses inside strings.
        private static int MatchingParen(string source, int start, int at)
        {
            int depth = 1;
            for (int i = start; i < source.Length; i++)
            {
                char c = source[i];
                if (c is '"' or '\'')
                {
                    i = SkipString(source, i);
                    continue;
                }
                if (c == '(')
                    depth++;
                else if (c == ')' && --depth == 0)
                    return i;
            }
            throw new ScriptSyntaxException("Unclosed '@('", at);
        }

        // name(.name | [ ... ])*  — a dot only when a name follows it.
        private static int PathEnd(string source, int start)
        {
            int i = start;
            while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                i++;

            while (i < source.Length)
            {
                if (source[i] == '.' && i + 1 < source.Length && (char.IsLetter(source[i + 1]) || source[i + 1] == '_'))
                {
                    i++;
                    while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                        i++;
                }
                else if (source[i] == '[')
                {
                    int depth = 0;
                    for (; i < source.Length; i++)
                    {
                        if (source[i] is '"' or '\'')
                            i = SkipString(source, i);
                        else if (source[i] == '[')
                            depth++;
                        else if (source[i] == ']' && --depth == 0)
                            break;
                    }
                    if (i >= source.Length)
                        throw new ScriptSyntaxException("Unclosed '['", start);
                    i++;
                }
                else
                {
                    break;
                }
            }

            return i;
        }

        private static int SkipString(string source, int i)
        {
            char quote = source[i++];
            while (i < source.Length && source[i] != quote)
                i += source[i] == '\\' ? 2 : 1;
            return Math.Min(i, source.Length - 1);
        }
    }
}
