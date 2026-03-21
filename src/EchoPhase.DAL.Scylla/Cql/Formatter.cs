// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text;

namespace EchoPhase.DAL.Scylla.Cql
{
    public static class CqlFormatter
    {
        private static readonly string[] Keywords = new[]
        {
            "SELECT", "FROM", "WHERE", "INSERT", "INTO", "UPDATE", "DELETE",
            "CREATE", "TABLE", "DROP", "ALTER", "WITH", "AND", "OR",
            "PRIMARY", "KEY", "CLUSTERING", "ORDER", "BY", "IF", "NOT", "EXISTS",
            "KEYSPACE", "INDEX", "TYPE", "VALUES", "SET", "LIMIT", "ALLOW", "FILTERING"
        };

        public static string Format(string cql, int indentSize = 6)
        {
            if (string.IsNullOrWhiteSpace(cql))
                return string.Empty;

            var tokens = Lexer.Tokenize(cql);
            return FormatTokens(tokens, indentSize);
        }

        private static string FormatTokens(List<Token> tokens, int indentSize)
        {
            var sb = new StringBuilder();
            var indent = new string(' ', indentSize);
            var currentLine = new List<Token>();
            var parenDepth = 0;
            var isInParentheses = false;

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                switch (token.Type)
                {
                    case TokenType.OpenParen:
                        parenDepth++;
                        isInParentheses = true;
                        currentLine.Add(token);
                        break;

                    case TokenType.CloseParen:
                        parenDepth--;
                        if (parenDepth == 0)
                            isInParentheses = false;
                        currentLine.Add(token);
                        break;

                    case TokenType.Keyword when !isInParentheses:
                        if (currentLine.Count > 0)
                        {
                            sb.AppendLine(indent + JoinTokens(currentLine));
                            currentLine.Clear();
                        }
                        currentLine.Add(token);
                        break;

                    default:
                        currentLine.Add(token);
                        break;
                }
            }

            if (currentLine.Count > 0)
            {
                sb.AppendLine(indent + JoinTokens(currentLine));
            }

            return sb.ToString().TrimEnd();
        }

        private static string JoinTokens(List<Token> tokens)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                var needsSpace = i > 0 &&
                                 tokens[i - 1].Type != TokenType.OpenParen &&
                                 token.Type != TokenType.CloseParen &&
                                 token.Type != TokenType.Comma &&
                                 token.Type != TokenType.Semicolon;

                if (needsSpace)
                    sb.Append(' ');

                sb.Append(token.Value);
            }

            return sb.ToString();
        }
    }
}
