using System.Text;

namespace EchoPhase.DAL.Scylla.Cql
{
    public static class Lexer
    {
        public static List<string> Lex(string cql)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(cql)) return tokens;

            var sb = new StringBuilder();
            foreach (char c in cql)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                    continue;
                }

                if ("(),;".Contains(c))
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                    tokens.Add(c.ToString());
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (sb.Length > 0)
                tokens.Add(sb.ToString());

            return tokens;
        }
    }
}
