using System.Text.RegularExpressions;

namespace EchoPhase.WebSockets.Exceptions
{
    public class MissingIntentsException : Exception
    {
        public MissingIntentsException(string template, params object[] args)
            : base(FormatMessage(template, args)) { }

        private static string FormatMessage(string template, object[] args)
        {
            var matches = Regex.Matches(template, @"\{.*?\}");

            if (matches.Count != args.Length)
                throw new InvalidOperationException(
                    $"Mismatch between number of placeholders ({matches.Count}) and arguments ({args.Length}).");

            for (int i = 0; i < matches.Count; i++)
            {
                template = ReplaceFirst(template, matches[i].Value, args[i]?.ToString() ?? "null");
            }

            return template;
        }

        private static string ReplaceFirst(string input, string search, string replace)
        {
            int pos = input.IndexOf(search);
            return pos < 0 ? input : input.Substring(0, pos) + replace + input.Substring(pos + search.Length);
        }
    }
}
