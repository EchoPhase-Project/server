// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;

namespace EchoPhase.Scripting.Engine
{
    // Record linkage after Fellegi and Sunter: which records are the same
    // thing told twice. Each field has m (it agrees when two records are the
    // same) and u (it agrees by chance when they are not); agreeing adds
    // log2(m/u) to a pair's score, disagreeing adds log2((1-m)/(1-u)), and a
    // missing value (null or blank) adds nothing. Above `upper` a pair is a
    // match, from `lower` a possible one for a person to look at.
    //
    //   fields: { email: { m: 0.95, u: 0.001 },
    //             name:  { m: 0.9, u: 0.01, compare: 'jaroWinkler', min: 0.85 },
    //             phone: { agree: 8, disagree: -3, key: p => replace(p.phone, ' ', '') } }
    //
    // compare: 'exact' (==, the default), 'jaroWinkler', 'levenshtein',
    // 'fuzzy' (case, accents, punctuation and word order aside), or
    // { within: n } for numbers. The text ones agree from `min` similarity,
    // as estimateWeights learns them; with `partial: true` the weight instead
    // rises from disagree at `min` to agree at 1 (Winkler's partial agreement).
    internal static partial class Functions
    {
        private static readonly Dictionary<string, Definition> Linkage = new(StringComparer.OrdinalIgnoreCase)
        {
            // duplicates(list, target, fields, options?): the records like `target` (an item, or its
            // index), best first: [{ item, index, score, status: 'match' | 'possible', fields }].
            ["duplicates"] = new(3, 4, a => FindDuplicates(a)),
            // duplicateGroups(list, fields, options?): { groups: [{ indices, items }], possible: [{ a, b, score }], pairs }.
            ["duplicateGroups"] = new(2, 3, a => FindGroups(a)),
            // estimateWeights(list, fields, options?): m and u learnt from the list (EM, no labels needed),
            // and the thresholds they imply: { fields, upper, lower, prevalence, pairs, iterations }.
            // Its `fields` go straight into duplicates and duplicateGroups, `upper` and `lower` into their options.
            ["estimateWeights"] = new(2, 3, a => EstimateWeights(a)),
        };

        private enum Comparison { Exact, JaroWinkler, Levenshtein, Fuzzy, Within }

        /// <summary>One field as configured; <see cref="Spec"/> is what it was given, for estimateWeights to hand back.</summary>
        private sealed record Field(
            string Name, ScriptFunction? Key, Comparison Compare, double Min, bool Partial, double Within,
            double Agree, double Disagree, IDictionary<string, object?> Spec);

        private static readonly IDictionary<string, object?> NoOptions = new Dictionary<string, object?>();

        // ── duplicates ───────────────────────────────────────────────────────

        private static List<object?> FindDuplicates(object?[] a)
        {
            const string Name = "duplicates";
            var records = ListOf(a, Name);
            var fields = Fields(a[2], Name, needWeights: true);
            var options = a.Length > 3 ? Object(a[3], Name) : NoOptions;
            var (upper, lower) = Thresholds(options, fields, Name);
            int limit = options.TryGetValue("limit", out var l) && l is not null ? Count(l) : int.MaxValue;

            int self = TargetIndex(records, a[1], Name);
            var target = self >= 0 ? records[self] : a[1];

            var found = new List<(object? Item, int Index, double Score, Dictionary<string, object?> Weights)>();
            for (int i = 0; i < records.Count; i++)
            {
                if (i == self)
                    continue;
                var weights = new Dictionary<string, object?>(fields.Count, StringComparer.Ordinal);
                double score = Score(fields, target, self, records[i], i, weights);
                if (score >= lower)
                    found.Add((records[i], i, score, weights));
            }

            return found
                .OrderByDescending(f => f.Score).ThenBy(f => f.Index)
                .Take(limit)
                .Select(f => (object?)new Dictionary<string, object?>
                {
                    ["item"] = f.Item,
                    ["index"] = (double)f.Index,
                    ["score"] = f.Score,
                    ["status"] = f.Score >= upper ? "match" : "possible",
                    ["fields"] = f.Weights,
                })
                .ToList();
        }

        // The target as a position in the list: a whole number is an index; an
        // item is found by identity (then excluded), else compared from outside.
        private static int TargetIndex(IList records, object? target, string name)
        {
            if (Values.IsNumber(Values.Unwrap(target)) && Values.ToNumber(target) is var n)
            {
                if (n != Math.Floor(n) || n < 0 || n >= records.Count)
                    throw new InvalidOperationException($"'{name}': index {Values.Format(n)} is not in the list of {records.Count}.");
                return (int)n;
            }
            for (int i = 0; i < records.Count; i++)
                if (ReferenceEquals(records[i], target))
                    return i;
            return -1;
        }

        // ── duplicateGroups ──────────────────────────────────────────────────

        private static Dictionary<string, object?> FindGroups(object?[] a)
        {
            const string Name = "duplicateGroups";
            var records = ListOf(a, Name);
            var fields = Fields(a[1], Name, needWeights: true);
            var options = a.Length > 2 ? Object(a[2], Name) : NoOptions;
            var (upper, lower) = Thresholds(options, fields, Name);

            var parent = Enumerable.Range(0, records.Count).ToArray();
            int Root(int i)
            {
                while (parent[i] != i)
                    i = parent[i] = parent[parent[i]];
                return i;
            }

            var possible = new List<object?>();
            int compared = 0;
            foreach (var (i, j) in Pairs(records, options, Name))
            {
                compared++;
                double score = Score(fields, records[i], i, records[j], j, weights: null);
                if (score >= upper)
                    parent[Root(i)] = Root(j); // same thing: join their groups (a ~ b ~ c is one group)
                else if (score >= lower)
                    possible.Add(new Dictionary<string, object?> { ["a"] = (double)i, ["b"] = (double)j, ["score"] = score });
            }

            var groups = Enumerable.Range(0, records.Count)
                .GroupBy(Root)
                .Where(g => g.Count() > 1)
                .Select(g => g.Order().ToList())
                .OrderBy(g => g[0])
                .Select(g => (object?)new Dictionary<string, object?>
                {
                    ["indices"] = g.Select(i => (object?)(double)i).ToList(),
                    ["items"] = g.Select(i => records[i]).ToList(),
                })
                .ToList();

            return new Dictionary<string, object?>
            {
                ["groups"] = groups,
                ["possible"] = possible,
                ["pairs"] = (double)compared,
            };
        }

        /// <summary>
        /// The pairs to compare (i &lt; j): all of them, or with `block` (a lambda,
        /// or a list of them) only those sharing a key under one of them. A
        /// missing key puts a record in no block for that lambda.
        /// </summary>
        private static IEnumerable<(int, int)> Pairs(IList records, IDictionary<string, object?> options, string name)
        {
            if (!options.TryGetValue("block", out var block) || block is null)
            {
                for (int i = 0; i < records.Count; i++)
                    for (int j = i + 1; j < records.Count; j++)
                        yield return (i, j);
                yield break;
            }

            var keys = Values.Unwrap(block) is IList list
                ? list.Cast<object?>().Select((k, i) => k as ScriptFunction ?? throw BadBlock(name)).ToList()
                : [block as ScriptFunction ?? throw BadBlock(name)];

            var seen = new HashSet<(int, int)>();
            foreach (var key in keys)
            {
                var blocks = new Dictionary<string, List<int>>(StringComparer.Ordinal);
                for (int i = 0; i < records.Count; i++)
                {
                    Budget.Spend(1);
                    var value = Values.Unwrap(key.Invoke(records[i], i));
                    if (IsMissing(value))
                        continue;
                    var text = Values.Format(value);
                    if (!blocks.TryGetValue(text, out var members))
                        blocks[text] = members = [];
                    members.Add(i);
                }
                foreach (var members in blocks.Values)
                    for (int x = 0; x < members.Count; x++)
                        for (int y = x + 1; y < members.Count; y++)
                            if (seen.Add((members[x], members[y])))
                                yield return (members[x], members[y]);
            }
        }

        private static InvalidOperationException BadBlock(string name)
            => new($"'{name}': block is a lambda giving a record's block key, or a list of them.");

        // ── estimateWeights ──────────────────────────────────────────────────

        private const int MaxIterations = 200;
        private const double Tolerance = 1e-6;
        private const double Floor = 1e-6;

        /// <summary>
        /// Expectation-maximisation over the compared pairs, as Winkler applied
        /// it to Fellegi-Sunter: each pair is a match with some probability;
        /// m, u and the share of matches are refined until they settle. A
        /// field agrees when its similarity reaches `min` (exact ones: equal);
        /// a missing value leaves the field out of that pair.
        /// </summary>
        private static Dictionary<string, object?> EstimateWeights(object?[] a)
        {
            const string Name = "estimateWeights";
            var records = ListOf(a, Name);
            var fields = Fields(a[1], Name, needWeights: false);
            var options = a.Length > 2 ? Object(a[2], Name) : NoOptions;
            double prevalence = options.TryGetValue("prevalence", out var p0) && p0 is not null ? Values.ToNumber(p0) : 0.1;
            if (!(prevalence > 0 && prevalence < 1))
                throw new InvalidOperationException($"'{Name}': prevalence (the share of pairs that match) is between 0 and 1.");

            // Each pair as agreement per field: 1, 0, or -1 for missing.
            var patterns = new List<sbyte[]>();
            foreach (var (i, j) in Pairs(records, options, Name))
            {
                var pattern = new sbyte[fields.Count];
                for (int f = 0; f < fields.Count; f++)
                {
                    var s = FieldSimilarity(fields[f], Value(fields[f], records[i], i), Value(fields[f], records[j], j));
                    pattern[f] = s is not { } similarity ? (sbyte)-1 : similarity >= fields[f].Min ? (sbyte)1 : (sbyte)0;
                }
                patterns.Add(pattern);
            }
            if (patterns.Count == 0)
                throw new InvalidOperationException($"'{Name}' needs at least one pair of records to compare.");

            // Start: m high; u near each field's agreement rate (most pairs are not matches).
            var m = new double[fields.Count];
            var u = new double[fields.Count];
            for (int f = 0; f < fields.Count; f++)
            {
                var observed = patterns.Where(x => x[f] >= 0).ToList();
                double rate = observed.Count == 0 ? 0.5 : observed.Count(x => x[f] == 1) / (double)observed.Count;
                m[f] = 0.9;
                u[f] = Math.Clamp(rate, 0.001, 0.5);
            }

            var g = new double[patterns.Count];
            int iterations = 0;
            for (; iterations < MaxIterations; iterations++)
            {
                // E: how likely each pair is a match.
                for (int k = 0; k < patterns.Count; k++)
                {
                    Budget.Spend(1);
                    double match = Math.Log(prevalence), other = Math.Log(1 - prevalence);
                    for (int f = 0; f < fields.Count; f++)
                    {
                        if (patterns[k][f] < 0)
                            continue;
                        bool agrees = patterns[k][f] == 1;
                        match += Math.Log(agrees ? m[f] : 1 - m[f]);
                        other += Math.Log(agrees ? u[f] : 1 - u[f]);
                    }
                    g[k] = 1 / (1 + Math.Exp(other - match));
                }

                // M: the parameters that make those likelihoods likeliest.
                double change = 0;
                double share = Math.Clamp(g.Average(), Floor, 1 - Floor);
                change = Math.Max(change, Math.Abs(share - prevalence));
                prevalence = share;
                for (int f = 0; f < fields.Count; f++)
                {
                    double agreeMatch = 0, match = 0, agreeOther = 0, other = 0;
                    for (int k = 0; k < patterns.Count; k++)
                    {
                        if (patterns[k][f] < 0)
                            continue;
                        match += g[k];
                        other += 1 - g[k];
                        if (patterns[k][f] == 1)
                        {
                            agreeMatch += g[k];
                            agreeOther += 1 - g[k];
                        }
                    }
                    double nextM = match > 0 ? Math.Clamp(agreeMatch / match, Floor, 1 - Floor) : m[f];
                    double nextU = other > 0 ? Math.Clamp(agreeOther / other, Floor, 1 - Floor) : u[f];
                    change = Math.Max(change, Math.Max(Math.Abs(nextM - m[f]), Math.Abs(nextU - u[f])));
                    (m[f], u[f]) = (nextM, nextU);
                }
                if (change < Tolerance)
                {
                    iterations++;
                    break;
                }
            }

            // The two classes are symmetric to EM: the match class is the one that agrees more.
            if (m.Sum() < u.Sum())
            {
                (m, u) = (u, m);
                prevalence = 1 - prevalence;
            }

            var estimated = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (int f = 0; f < fields.Count; f++)
            {
                // The field as given (key, compare, min), with the learnt weights.
                var spec = new Dictionary<string, object?>(fields[f].Spec, StringComparer.Ordinal);
                foreach (var weight in new[] { "m", "u", "agree", "disagree" })
                    spec.Remove(weight);
                spec["m"] = m[f];
                spec["u"] = u[f];
                spec["agree"] = Math.Log2(m[f] / u[f]);
                spec["disagree"] = Math.Log2((1 - m[f]) / (1 - u[f]));
                estimated[fields[f].Name] = spec;
            }

            // With the share of matches known, a pair is a match when that is
            // likelier than not (posterior over 1/2), possible from 1/10.
            double upper = Math.Log2((1 - prevalence) / prevalence);
            return new Dictionary<string, object?>
            {
                ["fields"] = estimated,
                ["upper"] = upper,
                ["lower"] = upper - Math.Log2(9),
                ["prevalence"] = prevalence,
                ["pairs"] = (double)patterns.Count,
                ["iterations"] = (double)iterations,
            };
        }

        // ── fields, values, scores ───────────────────────────────────────────

        private static List<Field> Fields(object? value, string name, bool needWeights)
        {
            var spec = Object(value, name);
            if (spec.Count == 0)
                throw new InvalidOperationException($"'{name}' needs at least one field to compare.");

            var fields = new List<Field>(spec.Count);
            foreach (var (fieldName, raw) in spec)
            {
                // { email: {} } or { email: true }: estimateWeights fills in the weights.
                var field = Values.Unwrap(raw) is bool or null ? new Dictionary<string, object?>() : Object(raw, name);
                string Where() => $"'{name}': field '{fieldName}'";

                var key = field.TryGetValue("key", out var k) && k is not null
                    ? k as ScriptFunction ?? throw new InvalidOperationException($"{Where()}: key is a lambda, like r => r.{fieldName}.")
                    : null;

                var (compare, within) = Compare(field.TryGetValue("compare", out var c) ? c : null, Where);
                double min = field.TryGetValue("min", out var mn) && mn is not null
                    ? Values.ToNumber(mn)
                    : compare switch
                    {
                        Comparison.JaroWinkler => 0.85,
                        Comparison.Levenshtein or Comparison.Fuzzy => 0.8,
                        _ => 1
                    };
                if (!(min > 0 && min <= 1))
                    throw new InvalidOperationException($"{Where()}: min is a similarity above 0, up to 1.");
                bool partial = field.TryGetValue("partial", out var pa) && Values.ToBool(pa);

                double agree = 0, disagree = 0;
                bool hasProbabilities = field.ContainsKey("m") || field.ContainsKey("u");
                bool hasWeights = field.ContainsKey("agree") || field.ContainsKey("disagree");
                if (hasWeights && field.TryGetValue("agree", out var ag) && field.TryGetValue("disagree", out var dg))
                {
                    (agree, disagree) = (Values.ToNumber(ag), Values.ToNumber(dg));
                }
                else if (hasProbabilities && field.TryGetValue("m", out var mv) && field.TryGetValue("u", out var uv))
                {
                    double m = Values.ToNumber(mv), u = Values.ToNumber(uv);
                    if (!(m > 0 && m < 1 && u > 0 && u < 1))
                        throw new InvalidOperationException($"{Where()}: m and u are probabilities strictly between 0 and 1.");
                    (agree, disagree) = (Math.Log2(m / u), Math.Log2((1 - m) / (1 - u)));
                }
                else if (needWeights || hasWeights || hasProbabilities)
                {
                    throw new InvalidOperationException($"{Where()} needs m and u, or agree and disagree (estimateWeights can learn them).");
                }

                fields.Add(new Field(fieldName, key, compare, min, partial, within, agree, disagree, field));
            }
            return fields;
        }

        private static (Comparison, double Within) Compare(object? value, Func<string> where)
        {
            switch (Values.Unwrap(value))
            {
                case null:
                    return (Comparison.Exact, 0);
                case string text:
                    return (text.ToLowerInvariant() switch
                    {
                        "exact" => Comparison.Exact,
                        "jarowinkler" or "jaro-winkler" => Comparison.JaroWinkler,
                        "levenshtein" => Comparison.Levenshtein,
                        "fuzzy" => Comparison.Fuzzy,
                        _ => throw new InvalidOperationException(
                            $"{where()}: compare '{text}' is not known; use exact, jaroWinkler, levenshtein, fuzzy or {{ within: n }}.")
                    }, 0);
                case IDictionary<string, object?> numeric when numeric.TryGetValue("within", out var w) && Values.ToNumber(w) is var within && within >= 0:
                    return (Comparison.Within, within);
                default:
                    throw new InvalidOperationException($"{where()}: compare is exact, jaroWinkler, levenshtein, fuzzy or {{ within: n }}.");
            }
        }

        // Without estimateWeights' thresholds: upper is half of all agreements
        // together, lower 0 (more for than against). A pair missing fields scores
        // less, so with gaps in the data prefer the estimated ones.
        private static (double Upper, double Lower) Thresholds(IDictionary<string, object?> options, List<Field> fields, string name)
        {
            double upper = options.TryGetValue("upper", out var up) && up is not null ? Values.ToNumber(up) : fields.Sum(f => f.Agree) / 2;
            double lower = options.TryGetValue("lower", out var lo) && lo is not null ? Values.ToNumber(lo) : Math.Min(0, upper);
            if (lower > upper)
                throw new InvalidOperationException($"'{name}': lower ({Values.Format(lower)}) is above upper ({Values.Format(upper)}).");
            return (upper, lower);
        }

        private static object? Value(Field field, object? record, int index)
        {
            if (field.Key is { } key)
                return Values.Unwrap(key.Invoke(record, index));
            return Values.Unwrap(record) is { } present ? Values.Unwrap(PathNode.Member(present, field.Name)) : null;
        }

        private static bool IsMissing(object? value) => value is null || (value is string s && string.IsNullOrWhiteSpace(s));

        /// <summary>From 0 to 1; null when either value is missing (the field tells nothing).</summary>
        private static double? FieldSimilarity(Field field, object? a, object? b)
        {
            Budget.Spend(1);
            if (IsMissing(a) || IsMissing(b))
                return null;
            return field.Compare switch
            {
                Comparison.Exact => Values.AreEqual(a, b) ? 1 : 0,
                Comparison.Within => Values.TryNumber(a, out var x) && Values.TryNumber(b, out var y)
                    ? (Math.Abs(x - y) <= field.Within ? 1 : 0)
                    : (Values.AreEqual(a, b) ? 1 : 0),
                Comparison.JaroWinkler => Similarity.JaroWinkler(Text(a).ToLowerInvariant(), Text(b).ToLowerInvariant()),
                Comparison.Levenshtein => Similarity.LevenshteinSimilarity(Text(a).ToLowerInvariant(), Text(b).ToLowerInvariant()),
                _ => Similarity.Fuzzy(Text(a), Text(b)),
            };
        }

        // From min: agree (partial: a share of the way from disagree at min to
        // agree at 1); below min: disagree; missing: nothing either way.
        private static double Weight(Field field, double? similarity)
        {
            if (similarity is not { } s)
                return 0;
            if (s >= 1)
                return field.Agree;
            if (s < field.Min)
                return field.Disagree;
            if (!field.Partial || field.Min >= 1)
                return field.Agree;
            return field.Disagree + (field.Agree - field.Disagree) * (s - field.Min) / (1 - field.Min);
        }

        private static double Score(List<Field> fields, object? a, int ia, object? b, int ib, Dictionary<string, object?>? weights)
        {
            double score = 0;
            foreach (var field in fields)
            {
                double weight = Weight(field, FieldSimilarity(field, Value(field, a, ia), Value(field, b, ib)));
                weights?.Add(field.Name, weight);
                score += weight;
            }
            return score;
        }
    }
}
