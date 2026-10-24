// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Text;

namespace EchoPhase.Scripting.Engine
{
    internal enum Tk
    {
        Number, String, Identifier,
        Plus, Minus, Star, Slash, Percent,
        Equal, NotEqual, Less, Greater, LessEq, GreaterEq,
        And, Or, Not,
        Question, Colon, Comma, Dot,
        LParen, RParen, LBracket, RBracket, LBrace, RBrace, Arrow,
        End
    }

    /// <param name="Position">Where it starts in the source, for error messages.</param>
    internal readonly record struct Lexeme(Tk Kind, string Text, double Number, int Position);

    /// <summary>A syntax error, with its position in the source.</summary>
    public sealed class ScriptSyntaxException(string message, int position)
        : FormatException($"{message} (at {position})")
    {
        public int Position { get; } = position;
    }

    /// <summary>Splits an expression into lexemes.</summary>
    internal static class Tokenizer
    {
        public static List<Lexeme> Tokenize(string source)
        {
            var result = new List<Lexeme>();
            // One instance per name: a parameter and its uses compare by reference.
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            int i = 0;

            while (true)
            {
                while (i < source.Length && char.IsWhiteSpace(source[i]))
                    i++;
                if (i >= source.Length)
                    break;

                int start = i;
                char c = source[i];

                if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < source.Length && char.IsAsciiDigit(source[i + 1])))
                {
                    result.Add(ReadNumber(source, ref i));
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                        i++;
                    var text = source[start..i];
                    if (!names.TryAdd(text, text))
                        text = names[text];
                    result.Add(new Lexeme(Keyword(text) ?? Tk.Identifier, text, 0, start));
                }
                else if (c is '"' or '\'')
                {
                    result.Add(ReadString(source, ref i));
                }
                else
                {
                    char next = i + 1 < source.Length ? source[i + 1] : '\0';
                    (Tk kind, int length) = (c, next) switch
                    {
                        ('=', '=') => (Tk.Equal, 2),
                        ('=', '>') => (Tk.Arrow, 2),
                        ('!', '=') => (Tk.NotEqual, 2),
                        ('<', '=') => (Tk.LessEq, 2),
                        ('>', '=') => (Tk.GreaterEq, 2),
                        ('&', '&') => (Tk.And, 2),
                        ('|', '|') => (Tk.Or, 2),
                        ('+', _) => (Tk.Plus, 1),
                        ('-', _) => (Tk.Minus, 1),
                        ('*', _) => (Tk.Star, 1),
                        ('/', _) => (Tk.Slash, 1),
                        ('%', _) => (Tk.Percent, 1),
                        ('<', _) => (Tk.Less, 1),
                        ('>', _) => (Tk.Greater, 1),
                        ('!', _) => (Tk.Not, 1),
                        ('?', _) => (Tk.Question, 1),
                        (':', _) => (Tk.Colon, 1),
                        (',', _) => (Tk.Comma, 1),
                        ('.', _) => (Tk.Dot, 1),
                        ('(', _) => (Tk.LParen, 1),
                        (')', _) => (Tk.RParen, 1),
                        ('[', _) => (Tk.LBracket, 1),
                        (']', _) => (Tk.RBracket, 1),
                        ('{', _) => (Tk.LBrace, 1),
                        ('}', _) => (Tk.RBrace, 1),
                        ('=', _) => throw new ScriptSyntaxException("Single '='; did you mean '=='?", i),
                        _ => throw new ScriptSyntaxException($"Unexpected character '{c}'", i)
                    };
                    i += length;
                    result.Add(new Lexeme(kind, source.Substring(start, length), 0, start));
                }
            }

            result.Add(new Lexeme(Tk.End, string.Empty, 0, source.Length));
            return result;
        }

        private static Tk? Keyword(string word) => word.ToLowerInvariant() switch
        {
            "and" => Tk.And,
            "or" => Tk.Or,
            "not" => Tk.Not,
            _ => null
        };

        private static Lexeme ReadNumber(string source, ref int i)
        {
            int start = i;
            bool dot = false;
            while (i < source.Length && (char.IsAsciiDigit(source[i]) || (source[i] == '.' && !dot && i + 1 < source.Length && char.IsAsciiDigit(source[i + 1]))))
            {
                dot |= source[i] == '.';
                i++;
            }

            // 1e3, 2.5E-4
            if (i < source.Length && source[i] is 'e' or 'E')
            {
                int mark = i++;
                if (i < source.Length && source[i] is '+' or '-')
                    i++;
                if (i >= source.Length || !char.IsAsciiDigit(source[i]))
                    throw new ScriptSyntaxException("Malformed exponent", mark);
                while (i < source.Length && char.IsAsciiDigit(source[i]))
                    i++;
            }

            if (i < source.Length && (char.IsLetter(source[i]) || source[i] == '_'))
                throw new ScriptSyntaxException("Malformed number", start);

            var text = source[start..i];
            return new Lexeme(Tk.Number, text, double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture), start);
        }

        // '...' or "...", with \\, \', \", \n, \t escapes.
        private static Lexeme ReadString(string source, ref int i)
        {
            int start = i;
            char quote = source[i++];
            var sb = new StringBuilder();

            while (i < source.Length && source[i] != quote)
            {
                char c = source[i++];
                if (c != '\\' || i >= source.Length)
                {
                    sb.Append(c);
                    continue;
                }

                char escaped = source[i++];
                sb.Append(escaped switch
                {
                    'n' => '\n',
                    't' => '\t',
                    _ => escaped
                });
            }

            if (i >= source.Length)
                throw new ScriptSyntaxException("Unterminated string", start);
            i++;
            return new Lexeme(Tk.String, sb.ToString(), 0, start);
        }
    }
}
