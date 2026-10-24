// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Text;

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// How alike two texts are, from 0 (nothing in common) to 1 (the same),
    /// for record linkage. Costs are charged to the evaluation's budget.
    /// </summary>
    internal static class Similarity
    {
        /// <summary>
        /// Jaro-Winkler as Winkler defined it: Jaro, then a boost for a common
        /// prefix of up to 4 characters (scale 0.1), only above Jaro 0.7.
        /// MARTHA/MARHTA 0.961, DWAYNE/DUANE 0.84, DIXON/DICKSONX 0.813.
        /// </summary>
        public static double JaroWinkler(string a, string b)
        {
            double jaro = Jaro(a, b);
            if (jaro <= 0.7)
                return jaro;

            int prefix = 0;
            for (int i = 0; i < Math.Min(4, Math.Min(a.Length, b.Length)) && a[i] == b[i]; i++)
                prefix++;
            return jaro + prefix * 0.1 * (1 - jaro);
        }

        public static double Jaro(string a, string b)
        {
            if (a.Length == 0 && b.Length == 0)
                return 1;
            if (a.Length == 0 || b.Length == 0)
                return 0;
            Budget.Spend(1 + a.Length * b.Length / 256);

            int window = Math.Max(0, Math.Max(a.Length, b.Length) / 2 - 1);
            var matchedA = new bool[a.Length];
            var matchedB = new bool[b.Length];
            int matches = 0;

            for (int i = 0; i < a.Length; i++)
            {
                int from = Math.Max(0, i - window), to = Math.Min(b.Length - 1, i + window);
                for (int j = from; j <= to; j++)
                {
                    if (matchedB[j] || a[i] != b[j])
                        continue;
                    matchedA[i] = matchedB[j] = true;
                    matches++;
                    break;
                }
            }
            if (matches == 0)
                return 0;

            // Half the matched characters that are out of order.
            int transposed = 0;
            for (int i = 0, j = 0; i < a.Length; i++)
            {
                if (!matchedA[i])
                    continue;
                while (!matchedB[j])
                    j++;
                if (a[i] != b[j])
                    transposed++;
                j++;
            }

            double m = matches;
            return (m / a.Length + m / b.Length + (m - transposed / 2.0) / m) / 3;
        }

        /// <summary>Edits (insert, delete, substitute) turning one text into the other.</summary>
        public static int Levenshtein(string a, string b)
        {
            if (a.Length == 0)
                return b.Length;
            if (b.Length == 0)
                return a.Length;
            Budget.Spend(1 + a.Length * b.Length / 256);

            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++)
                previous[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int substitute = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                    current[j] = Math.Min(substitute, Math.Min(previous[j] + 1, current[j - 1] + 1));
                }
                (previous, current) = (current, previous);
            }
            return previous[b.Length];
        }

        /// <summary>1 - edits / longer length: kitten/sitting is 1 - 3/7.</summary>
        public static double LevenshteinSimilarity(string a, string b)
        {
            int longer = Math.Max(a.Length, b.Length);
            return longer == 0 ? 1 : 1 - (double)Levenshtein(a, b) / longer;
        }

        /// <summary>
        /// Levenshtein similarity after taking away what people vary on
        /// without meaning another thing: case, accents, punctuation, spacing
        /// and word order. "Smith, John" and "john  SMITH" are the same; so
        /// are "José" and "Jose".
        /// </summary>
        public static double Fuzzy(string a, string b) => LevenshteinSimilarity(Normalize(a), Normalize(b));

        /// <summary>Lower case, no accents, words of letters and digits, sorted, single-spaced.</summary>
        public static string Normalize(string text)
        {
            var plain = new StringBuilder(text.Length);
            foreach (var c in text.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;
                plain.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
            }

            var words = plain.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Array.Sort(words, StringComparer.Ordinal);
            return string.Join(' ', words);
        }
    }
}
