// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Scripting.Engine;

namespace EchoPhase.Scripting.Tests
{
    /// <summary>Similarity measures and Fellegi-Sunter linkage: duplicates, duplicateGroups, estimateWeights.</summary>
    public class LinkageTests
    {
        private static Dictionary<string, object?> Person(string? name, string? email, string? city, double? year = null)
            => new() { ["name"] = name, ["email"] = email, ["city"] = city, ["year"] = year };

        private static object? Run(string expression, Dictionary<string, object?> vars)
            => Eval.EvaluateExpression(expression, vars);

        private static Dictionary<string, object?> AsObject(object? value)
            => Assert.IsAssignableFrom<Dictionary<string, object?>>(value);

        private static List<object?> AsList(object? value) => Assert.IsAssignableFrom<List<object?>>(value);

        // ── similarity ───────────────────────────────────────────────────────

        // Winkler's (and every library's) reference values.
        [Theory]
        [InlineData("MARTHA", "MARHTA", 0.9611)]
        [InlineData("DWAYNE", "DUANE", 0.84)]
        [InlineData("DIXON", "DICKSONX", 0.8133)]
        [InlineData("same", "same", 1)]
        [InlineData("", "", 1)]
        [InlineData("abc", "", 0)]
        [InlineData("abc", "xyz", 0)]
        public void JaroWinkler_MatchesTheReferenceValues(string a, string b, double expected)
            => Assert.Equal(expected, Similarity.JaroWinkler(a, b), 4);

        [Fact]
        public void Levenshtein_CountsEdits()
        {
            Assert.Equal(3, Similarity.Levenshtein("kitten", "sitting"));
            Assert.Equal(0, Similarity.Levenshtein("same", "same"));
            Assert.Equal(4, Similarity.Levenshtein("", "four"));
            Assert.Equal(1 - 3.0 / 7, Similarity.LevenshteinSimilarity("kitten", "sitting"), 10);
        }

        [Theory]
        [InlineData("Smith, John", "john  SMITH")]
        [InlineData("José Álvarez", "jose alvarez")]
        [InlineData("O'Neil-Smith", "smith o neil")]
        public void Fuzzy_IgnoresCaseAccentsPunctuationAndWordOrder(string a, string b)
            => Assert.Equal(1, Similarity.Fuzzy(a, b));

        [Fact]
        public void Fuzzy_StillTellsDifferentNamesApart()
            => Assert.InRange(Similarity.Fuzzy("John Smith", "Joan Smyth"), 0.7, 0.9);

        // ── duplicates ───────────────────────────────────────────────────────

        private static Dictionary<string, object?> People() => new()
        {
            ["people"] = new List<object?>
            {
                Person("John Smith", "john@x.io", "Riga"),        // 0: the one we look for
                Person("Jon Smith", "john@x.io", "Riga"),         // 1: a typo, same email
                Person("Smith, John", null, "Riga"),              // 2: no email, name reordered
                Person("Anna Berg", "anna@x.io", "Riga"),         // 3: same city only
                Person("John Smith", "j.smith@y.io", "Oslo"),     // 4: a namesake elsewhere
            },
        };

        private const string Fields = """
            { email: { m: 0.95, u: 0.001 },
              name:  { m: 0.9,  u: 0.01, compare: 'fuzzy', partial: true },
              city:  { m: 0.8,  u: 0.25 } }
            """;

        [Fact]
        public void Duplicates_ScoreByLogLikelihood_BestFirst_WithoutTheTarget()
        {
            var vars = People();
            // 1: email + city + a near name (~13.2); 2: name + city, no email (~8.2).
            var found = AsList(Run($"duplicates(people, people[0], {Fields}, {{ upper: 9, lower: 2 }})", vars));

            var indices = found.Select(f => (double)Must.Present(AsObject(f)["index"])).ToList();
            Assert.Equal([1d, 2d], indices); // 3 and 4 are under lower; 0 is the target
            var best = AsObject(found[0]);
            Assert.Equal("match", best["status"]);

            // Weights by hand: agree log2(m/u), disagree log2((1-m)/(1-u)).
            double email = Math.Log2(0.95 / 0.001), city = Math.Log2(0.8 / 0.25);
            var weights = AsObject(best["fields"]);
            Assert.Equal(email, (double)Must.Present(weights["email"]), 10);
            Assert.Equal(city, (double)Must.Present(weights["city"]), 10);
            // "Jon Smith" is 0.9 alike: 0.5 of the way from min 0.8 to 1.
            double agree = Math.Log2(0.9 / 0.01), disagree = Math.Log2(0.1 / 0.99);
            double name = disagree + (agree - disagree) * (Similarity.Fuzzy("Jon Smith", "John Smith") - 0.8) / 0.2;
            Assert.Equal(name, (double)Must.Present(weights["name"]), 10);
            Assert.Equal(email + city + name, (double)Must.Present(best["score"]), 10);

            // A missing email tells nothing: 0, not a disagreement.
            var second = AsObject(found[1]);
            Assert.Equal(0d, AsObject(second["fields"])["email"]);
            Assert.Equal("possible", second["status"]);
        }

        [Fact]
        public void Duplicates_TakeAnIndex_ALimit_AndAnOutsideRecord()
        {
            var vars = People();
            Assert.Single(AsList(Run($"duplicates(people, 0, {Fields}, {{ upper: 9, lower: 2, limit: 1 }})", vars)));

            // A record not in the list is compared with all of them.
            var outside = AsList(Run($"duplicates(people, {{ name: 'john smith', email: 'john@x.io', city: 'Riga' }}, {Fields}, {{ lower: 2 }})", vars));
            Assert.Equal(0d, AsObject(outside[0])["index"]);
        }

        [Fact]
        public void Duplicates_UseKeysAndNumericTolerance()
        {
            var vars = new Dictionary<string, object?>
            {
                ["rows"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["phone"] = "+371 2000 0000", ["amount"] = 10.0 },
                    new Dictionary<string, object?> { ["phone"] = "+37120000000", ["amount"] = 10.004 },
                    new Dictionary<string, object?> { ["phone"] = "+37120000001", ["amount"] = 12.0 },
                },
            };
            var found = AsList(Run("""
                duplicates(rows, 0, {
                    phone:  { agree: 8, disagree: -4, key: r => replace(r.phone, ' ', '') },
                    amount: { agree: 2, disagree: -1, compare: { within: 0.01 } } })
                """, vars));
            var best = AsObject(found[0]);
            Assert.Equal(1d, best["index"]);
            Assert.Equal(10d, best["score"]);
        }

        [Fact]
        public void NearValues_AgreeFully_UnlessPartial()
        {
            var vars = People();
            double Name(string options) => (double)Must.Present(AsObject(AsObject(AsList(Run(
                $"duplicates(people, 0, {{ name: {{ agree: 6, disagree: -4, compare: 'fuzzy'{options} }} }}, {{ lower: -100 }})", vars))
                .Single(f => (double)Must.Present(AsObject(f)["index"]) == 1))["fields"])["name"]);

            Assert.Equal(6, Name(""));                                   // 0.9 >= min 0.8: agrees
            Assert.Equal(1, Name(", partial: true"), 10);                // halfway from -4 to 6
            Assert.Equal(-4, Name(", min: 0.95"));                       // under min: disagrees
        }

        [Theory]
        [InlineData("duplicates(people, 0, { name: { m: 0.9 } })", "needs m and u")]
        [InlineData("duplicates(people, 0, { name: { m: 1, u: 0.1 } })", "strictly between 0 and 1")]
        [InlineData("duplicates(people, 0, { name: { agree: 1, disagree: 0, compare: 'soundex' } })", "is not known")]
        [InlineData("duplicates(people, 0, { name: { agree: 1, disagree: 0, key: 5 } })", "key is a lambda")]
        [InlineData("duplicates(people, 9, { name: { agree: 1, disagree: 0 } })", "not in the list")]
        [InlineData("duplicates(people, 0, {})", "at least one field")]
        [InlineData("duplicates(people, 0, { name: { agree: 1, disagree: 0 } }, { upper: 1, lower: 5 })", "is above upper")]
        public void Duplicates_ExplainBadFields(string expression, string message)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Run(expression, People()));
            Assert.Contains(message, ex.Message);
        }

        // ── duplicateGroups ──────────────────────────────────────────────────

        [Fact]
        public void Groups_JoinMatchesTransitively_AndListPossibleOnes()
        {
            var vars = new Dictionary<string, object?>
            {
                ["people"] = new List<object?>
                {
                    Person("Ann Lee", "a@x.io", "Riga"),       // 0 ─ email ─ 1
                    Person("Anne Lee", "a@x.io", "Riga"),      // 1 ─ name  ─ 2
                    Person("Anne Lee", "anne@y.io", "Riga"),   // 2: to 1, 6 - 2 + 1 = 5
                    Person("Bob Ray", "b@x.io", "Oslo"),       // 3
                    Person("Bob Ray", null, "Bergen"),         // 4 possibly 3: 6 + 0 - 2 = 4
                },
            };
            var result = AsObject(Run("""
                duplicateGroups(people, {
                    email: { agree: 10, disagree: -2 },
                    name:  { agree: 6, disagree: -6, compare: 'jaroWinkler', min: 0.9 },
                    city:  { agree: 1, disagree: -2 } }, { upper: 5, lower: 2 })
                """, vars));

            var group = AsObject(Assert.Single(AsList(result["groups"])));
            Assert.Equal([0d, 1d, 2d], AsList(group["indices"]).Cast<double>());
            var possible = AsObject(Assert.Single(AsList(result["possible"])));
            Assert.Equal((3d, 4d), ((double)Must.Present(possible["a"]), (double)Must.Present(possible["b"])));
            Assert.Equal(10d, result["pairs"]); // 5 records: all 10 pairs
        }

        [Fact]
        public void Blocking_ComparesOnlyWithinBlocks()
        {
            var vars = new Dictionary<string, object?>
            {
                ["people"] = new List<object?>
                {
                    Person("Ann", "1", "Riga"), Person("Ann", "1", "Riga"), Person("Bob", "2", "Oslo"),
                    Person("Bo", "2", "Oslo"), Person("Cy", "3", null),
                },
            };
            const string Spec = "{ email: { agree: 10, disagree: -2 } }";
            var byCity = AsObject(Run($"duplicateGroups(people, {Spec}, {{ upper: 5, block: p => p.city }})", vars));
            Assert.Equal(2d, byCity["pairs"]);           // (0,1) in Riga, (2,3) in Oslo; Cy has no city
            Assert.Equal(2, AsList(byCity["groups"]).Count);

            var twoKeys = AsObject(Run($"duplicateGroups(people, {Spec}, {{ upper: 5, block: [p => p.city, p => p.email] }})", vars));
            Assert.Equal(2d, twoKeys["pairs"]);          // the same pairs again are not counted twice
        }

        // ── estimateWeights ──────────────────────────────────────────────────

        // 300 people in 5 cities, names from 30 first and 40 last ones made of
        // random syllables (few namesakes, as in real data), a birth year each; 60 of them entered
        // twice with a typo in the name, now and then no email, once in a
        // while a year off by one. EM must find which fields tell duplicates
        // apart without being told which pairs are ones.
        private static (List<object?> People, HashSet<(int, int)> Twins) Synthetic()
        {
            var random = new Random(7);
            string[] cities = ["Riga", "Oslo", "Bergen", "Tartu", "Turku"];
            // Names of random syllables: as unlike each other as real ones.
            string Word(int syllables)
            {
                const string Consonants = "bdfgklmnprstvz", Vowels = "aeiou";
                var word = new System.Text.StringBuilder();
                for (int k = 0; k < syllables; k++)
                    word.Append(Consonants[random.Next(Consonants.Length)]).Append(Vowels[random.Next(Vowels.Length)]);
                return char.ToUpperInvariant(word[0]) + word.ToString()[1..];
            }
            var first = Enumerable.Range(0, 30).Select(_ => Word(2 + random.Next(2))).ToArray();
            var last = Enumerable.Range(0, 40).Select(_ => Word(3 + random.Next(2))).ToArray();
            var people = new List<object?>();
            var twins = new HashSet<(int, int)>();

            for (int i = 0; i < 300; i++)
            {
                var name = $"{first[random.Next(first.Length)]} {last[random.Next(last.Length)]}";
                var email = $"user{i}@mail.test";
                var city = cities[random.Next(cities.Length)];
                double year = 1950 + random.Next(55);
                people.Add(Person(name, email, city, year));
                if (i < 60)
                {
                    // A letter dropped inside the first or the last name (not the space between).
                    int at;
                    do
                        at = 1 + random.Next(name.Length - 2);
                    while (name[at] == ' ');
                    var typo = name.Remove(at, 1);
                    var copyYear = random.Next(10) == 0 ? year + 1 : year;
                    people.Add(Person(typo, random.Next(4) == 0 ? null : email, city, copyYear));
                    twins.Add((people.Count - 2, people.Count - 1));
                }
            }
            return (people, twins);
        }

        [Fact]
        public void EstimateWeights_LearnsWhichFieldsTellDuplicates_AndThoseWeightsFindThem()
        {
            var (people, twins) = Synthetic();
            var vars = new Dictionary<string, object?> { ["people"] = people };
            // First and last name apart: Jaro-Winkler on the whole name would
            // take two people with the same first name for alike.
            const string Spec = """
                { email: {},
                  first: { compare: 'jaroWinkler', min: 0.9, key: p => split(p.name, ' ')[0] },
                  last:  { compare: 'jaroWinkler', min: 0.9, key: p => split(p.name, ' ')[1] },
                  year: {}, city: {} }
                """;

            var estimate = AsObject(Run($"estimateWeights(people, {Spec}, {{ block: p => p.city }})", vars));
            var fields = AsObject(estimate["fields"]);
            var email = AsObject(fields["email"]);
            var name = AsObject(fields["last"]);
            var city = AsObject(fields["city"]);

            var year = AsObject(fields["year"]);

            // Email: duplicates always agree (when they have one), others never.
            Assert.InRange((double)Must.Present(email["m"]), 0.95, 1);
            Assert.InRange((double)Must.Present(email["u"]), 0, 0.001);
            // Last name: a typo still agrees from 0.9 (the typo is in one of the two
            // names); strangers rarely look alike.
            Assert.InRange((double)Must.Present(name["m"]), 0.9, 1);
            Assert.InRange((double)Must.Present(name["u"]), 0, 0.05);
            // Year: one copy in ten is off by one; 55 years to share by chance.
            Assert.InRange((double)Must.Present(year["m"]), 0.8, 0.98);
            Assert.InRange((double)Must.Present(year["u"]), 0.01, 0.03);
            // About as many matching pairs as there are twins.
            Assert.InRange((double)Must.Present(estimate["prevalence"]) * (double)Must.Present(estimate["pairs"]), 50, 70);
            // Blocked by city: every pair agrees on it, so it tells nothing.
            Assert.True(Math.Abs((double)Must.Present(city["agree"])) < 0.1);
            // The settings given are handed back with the weights.
            Assert.Equal("jaroWinkler", name["compare"]);
            Assert.Equal(0.9, name["min"]);

            // Matches are rare here: likelier than not means a high score.
            Assert.InRange((double)Must.Present(estimate["upper"]), 6, 10);
            Assert.Equal((double)Must.Present(estimate["upper"]) - Math.Log2(9), (double)Must.Present(estimate["lower"]), 10);

            vars["estimate"] = estimate;
            var result = AsObject(Run(
                "duplicateGroups(people, estimate.fields, { block: p => p.city, upper: estimate.upper, lower: estimate.lower })", vars));
            var found = AsList(result["groups"])
                .Select(g => AsList(AsObject(g)["indices"]).Cast<double>().Select(d => (int)d).ToList())
                .ToList();

            int recovered = twins.Count(t => found.Any(g => g.Contains(t.Item1) && g.Contains(t.Item2)));
            // Nearly all twins: a few lose too much to a typo in a short name
            // together with a missing email, and are rightly not called a match.
            Assert.InRange(recovered, (int)(twins.Count * 0.9), twins.Count);
            Assert.All(found, g => Assert.InRange(g.Count, 2, 3));                       // no lumping together
            Assert.InRange(found.Count(g => g.Count != 2 || !twins.Contains((g[0], g[1]))), 0, 3); // hardly any strangers
        }

        [Fact]
        public void EstimateWeights_NeedsPairs_AndAPlausiblePrevalence()
        {
            var vars = new Dictionary<string, object?> { ["one"] = new List<object?> { Person("A", "a", "x") } };
            Assert.Contains("at least one pair", Assert.Throws<InvalidOperationException>(() => Run("estimateWeights(one, { email: {} })", vars)).Message);
            Assert.Contains("prevalence", Assert.Throws<InvalidOperationException>(() => Run("estimateWeights(one, { email: {} }, { prevalence: 2 })", vars)).Message);
        }

        [Fact]
        public void AllPairsOfALargeList_StopAtTheBudget()
        {
            var vars = new Dictionary<string, object?>
            {
                ["many"] = Enumerable.Range(0, 3000).Select(i => (object?)Person($"n{i}", $"e{i}", "c")).ToList()
            };
            var ex = Assert.Throws<InvalidOperationException>(() => Run("duplicateGroups(many, { email: { agree: 9, disagree: -1 } })", vars));
            Assert.Contains("operations", ex.Message);
        }
    }
}
