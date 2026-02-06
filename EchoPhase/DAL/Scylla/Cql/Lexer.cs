using System.Text;

namespace EchoPhase.DAL.Scylla.Cql
{
    public static class Lexer
    {
        private static readonly string[] Keywords = new[]
        {
            "SELECT", "FROM", "WHERE", "INSERT", "INTO", "UPDATE", "DELETE",
            "CREATE", "TABLE", "DROP", "ALTER", "WITH", "AND", "OR",
            "PRIMARY", "KEY", "CLUSTERING", "ORDER", "BY", "IF", "NOT", "EXISTS",
            "KEYSPACE", "INDEX", "TYPE", "VALUES", "SET", "LIMIT", "ALLOW", "FILTERING"
        };

        public static List<Token> Tokenize(string cql)
        {
            var tokens = new List<Token>();
            var current = new StringBuilder();
            var inString = false;
            var stringChar = '\0';

            for (int i = 0; i < cql.Length; i++)
            {
                char c = cql[i];

                if (inString)
                {
                    current.Append(c);
                    if (c == stringChar && (i == 0 || cql[i - 1] != '\\'))
                    {
                        tokens.Add(new Token(TokenType.String, current.ToString()));
                        current.Clear();
                        inString = false;
                    }
                    continue;
                }

                if (c == '\'' || c == '"')
                {
                    if (current.Length > 0)
                    {
                        AddToken(tokens, current.ToString());
                        current.Clear();
                    }
                    inString = true;
                    stringChar = c;
                    current.Append(c);
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        AddToken(tokens, current.ToString());
                        current.Clear();
                    }
                    continue;
                }

                if (c == '(' || c == ')' || c == ',' || c == ';')
                {
                    if (current.Length > 0)
                    {
                        AddToken(tokens, current.ToString());
                        current.Clear();
                    }

                    tokens.Add(new Token(c switch
                    {
                        '(' => TokenType.OpenParen,
                        ')' => TokenType.CloseParen,
                        ',' => TokenType.Comma,
                        ';' => TokenType.Semicolon,
                        _ => TokenType.Unknown
                    }, c.ToString()));
                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
            {
                AddToken(tokens, current.ToString());
            }

            return tokens;
        }

        private static void AddToken(List<Token> tokens, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var upperValue = value.ToUpperInvariant();
            var type = Keywords.Contains(upperValue) ? TokenType.Keyword :
                       char.IsDigit(value[0]) ? TokenType.Number :
                       TokenType.Identifier;

            tokens.Add(new Token(type, value));
        }

        // Legacy compatibility
        public static List<string> Lex(string cql)
        {
            return Tokenize(cql).Select(t => t.Value).ToList();
        }
    }
}
