using System.Text;

namespace EchoPhase.DAL.Scylla.Cql
{
    public static class CqlFormatter
    {
        public static string FormatCql(string cql)
        {
            var tokens = Lexer.Lex(cql);
            var sb = new StringBuilder();
            var currentLine = new List<string>();
            bool inParams = false;
            int parenLevel = 0;

            int i = 0;
            while (i < tokens.Count)
            {
                string token = tokens[i];

                if (token == "(") { inParams = true; parenLevel++; }
                if (token == ")") { parenLevel--; if (parenLevel <= 0) { inParams = false; parenLevel = 0; } }

                if (!inParams && IsUpper(token))
                {
                    if (currentLine.Count > 0)
                    {
                        sb.AppendLine("      " + JoinTokens(currentLine));
                        currentLine.Clear();
                    }

                    var commandTokens = new List<string> { token };
                    int j = i + 1;
                    while (j < tokens.Count && !inParams && IsUpper(tokens[j]))
                    {
                        commandTokens.Add(tokens[j]);
                        j++;
                    }
                    currentLine.AddRange(commandTokens);
                    i = j - 1;
                }
                else
                {
                    currentLine.Add(token);
                }

                i++;
            }

            if (currentLine.Count > 0)
                sb.AppendLine("      " + JoinTokens(currentLine));

            return sb.ToString().TrimEnd();
        }

        private static string JoinTokens(List<string> tokens)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];

                if (token == "(")
                {
                    sb.Append(token);
                }
                else if (token == ")" || token == ";")
                {
                    sb.Append(token);
                }
                else if (token == ",")
                {
                    sb.Append(token);
                }
                else
                {
                    if (i > 0 && tokens[i - 1] != "(") sb.Append(" ");
                    sb.Append(token);
                }
            }
            return sb.ToString();
        }

        private static bool IsUpper(string token)
        {
            foreach (char c in token)
                if (!char.IsUpper(c)) return false;
            return true;
        }
    }
}
