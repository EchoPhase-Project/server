// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Scripting.Engine;

namespace EchoPhase.Scripting.Tests
{
    /// <summary>
    /// The compiled path against the interpreter, expression by expression:
    /// the same value or the same error, as an object and as a number.
    /// Compiles directly (no fallback), so a compile failure fails here too.
    /// </summary>
    public class CompilerTests
    {
        private static Dictionary<string, object> Vars() => new()
        {
            ["n"] = 7d,
            ["zero"] = 0d,
            ["neg"] = -2.5,
            ["nan"] = double.NaN,
            ["big"] = 1e300,
            ["s"] = "hello",
            ["num"] = "12",
            ["empty"] = "",
            ["t"] = true,
            ["f"] = false,
            ["xs"] = new List<object?> { 3d, 1d, 2d },
            ["user"] = new Dictionary<string, object?> { ["name"] = "Ann", ["age"] = 31d, ["tags"] = new List<object?> { "a", "b" }, ["boss"] = null },
            ["users"] = new List<object?>
            {
                new Dictionary<string, object?> { ["name"] = "Ann", ["age"] = 31d },
                new Dictionary<string, object?> { ["name"] = "Bob", ["age"] = 17d },
            },
        };

        public static TheoryData<string> Expressions =>
        [
            // literals, numbers, arithmetic (unboxed), errors in arithmetic
            "1", "-n", "n * 2 + 1", "n / 2 - n % 3", "(n + 1) * (n - 1)", "n / zero", "n % zero", "-neg * 2",
            "big * big", "nan + 1", "num * 2", "s * 2", "t + 1", "user.boss + 1", "num + 1", "s + 1", "n + s", "xs + 1",
            // comparisons: numeric, mixed, general, NaN, ordering errors
            "n > 5", "n >= 7 && n <= 7", "n == 7", "n != 7", "n == num", "n < num", "num > n", "n == s", "n < s", "s > n",
            "n == user.boss", "user.boss == n", "n < user.boss", "nan == nan", "nan < 1", "1 > nan", "n == t", "1 == t", "0 == f",
            "s == 'hello'", "s < 'z'", "'a' < 1", "xs == xs", "s > xs",
            // logic, short-circuit (the right side would fail)
            "t && f", "t || missing", "f && missing", "!t", "!n", "!empty", "not n == 7", "n > 1 ? 'big' : 'small'",
            "f ? missing : n * 2", "t ? n : missing", "user.boss ? 1 : 2",
            // variables and paths, nulls on the way, unknown variables
            "n", "missing", "user.name", "user.age * 2", "user.tags[1]", "user.tags[-1]", "user.tags[5]", "user.boss.name",
            "user.boss[missing]", "user.nothing.deeper", "users[0].name", "users[n - 6].age", "s[1]", "xs[1.5]",
            "user[s]", "n.x", "xs['a']",
            // calls: pure, with lambdas (interpreted inside), errors
            "len(xs)", "upper(user.name)", "max(n, 2, 9)", "sum(xs)", "sum(users, u => u.age)", "map(xs, x => x * n)",
            "filter(users, u => u.age >= 18)[0].name", "len(missing)", "abs(s)", "coalesce(user.boss, user.nothing, n)",
            "contains(user.tags, 'b')", "'b' in user.tags", "'z' not in s", "round(n / 3, 2)",
            // forms the compiler hands back to the interpreter
            "[n, n * 2]", "{ a: n, b: s }", "[x * 2 for x in xs if x > 1]", "xs[1:]", "s[::-1]", "(x => x + n)",
            "sort(xs)[0] + users[1].age",
        ];

        private static string Outcome(Func<object?> evaluate)
        {
            try
            {
                var value = evaluate();
                return value is null ? "null" : $"{Eval.Format(value)} ({value.GetType().Name})";
            }
            catch (Exception ex)
            {
                return $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        [Theory]
        [MemberData(nameof(Expressions))]
        public void Compiled_GivesWhatTheInterpreterGives(string source)
        {
            var node = ExpressionParser.Parse(source);
            var value = Compiler.CompileValue(node);
            var number = Compiler.CompileNumber(node);

            Assert.Equal(Outcome(() => node.Evaluate(Vars())), Outcome(() => value(Vars())));
            Assert.Equal(Outcome(() => node.EvaluateNumber(Vars())), Outcome(() => number(Vars())));
        }

        [Fact]
        public void HotExpressions_AreCompiled_AndKeepTheirAnswers()
        {
            var vars = Vars();
            const string Source = "user.age * 2 + n > 10 && user.name != 'x'";
            var node = (TieredNode)TieredNode.Wrap(ExpressionParser.Parse(Source));
            var first = node.Evaluate(vars);
            for (int i = 0; i < TieredNode.Threshold * 2; i++)
                Assert.Equal(first, node.Evaluate(vars));
        }

        [Fact]
        public void ALambdaBody_IsCompiledWhenHot()
        {
            var vars = new Dictionary<string, object>
            {
                ["xs"] = Enumerable.Range(0, 1000).Select(i => (object?)(double)i).ToList()
            };
            // The first 100 items interpreted, the rest compiled: one consistent answer.
            Assert.Equal(1000 * 999d, Eval.TypedExecute<double>("sum(map(xs, x => x * 2 - (x > 500 ? 0 : 0)))", vars));
            Assert.Equal(499d, Eval.TypedExecute<double>("count(xs, x => x % 2 == 1 && x < 998)", vars));
        }
    }
}
