// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tests
{
    /// <summary>Behaviour of the compiled engine, including what the old one got wrong.</summary>
    public class EngineTests
    {
        private static Dictionary<string, object> Vars(params (string Key, object Value)[] pairs)
            => pairs.ToDictionary(p => p.Key, p => p.Value);

        private static object? Run(string expression, Dictionary<string, object>? vars = null)
            => Eval.TypedExecute<object>(expression, vars ?? []);

        private static string Render(string template, Dictionary<string, object>? vars = null)
            => Eval.TypedProcess<string>(template, vars ?? []);

        [Fact]
        public void AndOr_ShortCircuit()
        {
            // The right side would fail (unknown variable, division by zero) if it ran.
            Assert.Equal(false, Run("false && missing.x"));
            Assert.Equal(true, Run("true || 1 / 0"));
        }

        [Fact]
        public void Ternary_EvaluatesOnlyTheChosenBranch()
        {
            var vars = Vars(("x", 0L));
            Assert.Equal(0d, Run("x != 0 ? 10 / x : 0", vars));
        }

        [Fact]
        public void Comparisons_AreBooleans_AndWorkWithBoolVariables()
        {
            Assert.Equal(true, Run("2 > 1"));
            Assert.Equal(true, Run("flag == true", Vars(("flag", true))));
            Assert.Equal("yes: true", Render("yes: @(2 > 1)"));
        }

        [Fact]
        public void Null_IsAValue()
        {
            var vars = Vars(("user", new Dictionary<string, object?> { ["name"] = null }));
            Assert.Equal(true, Run("user.name == null", vars));
            Assert.Equal(true, Run("user.missing.deeper == null", vars));
            Assert.Equal(false, Run("0 == null"));
        }

        [Fact]
        public void Plus_JoinsText_AndAddsNumbers()
        {
            Assert.Equal("ab", Run("'a' + 'b'"));
            Assert.Equal("n=3", Run("'n=' + 3"));
            Assert.Equal(6d, Run("'5' + 1"));
        }

        [Fact]
        public void DivisionByZero_IsAnError()
        {
            var result = Eval.Execute<double>("1 / 0", new Dictionary<string, object>());
            Assert.True(result.TryGetError(out var error));
            Assert.Equal("DivideByZeroException", error!.Code);
        }

        [Fact]
        public void Functions_CheckArgumentCountsWhenParsed()
        {
            Assert.Equal(3d, Run("max(1, 3, 2)"));
            Assert.Equal(2.35, Run("round(2.345, 2)"));
            Assert.Equal(3d, Run("len(items)", Vars(("items", new List<object?> { 1L, 2L, 3L }))));
            Assert.Equal(true, Run("contains(lower('Hello'), 'ell')"));
            Assert.Equal("fallback", Run("coalesce(null, 'fallback')"));

            Assert.Contains("takes 1 argument", Eval.CheckExpression("abs(1, 2)"));
            Assert.Contains("Unknown function", Eval.CheckExpression("explode(1)"));
        }

        [Fact]
        public void Indexes_ListsFromTheEnd_AndDictionariesByKey()
        {
            var vars = Vars(
                ("items", new List<object?> { "a", "b", "c" }),
                ("blocks", new Dictionary<string, object?> { ["b3"] = 7L }));

            Assert.Equal("c", Run("items[-1]", vars));
            Assert.Equal(7L, Run("blocks['b3']", vars));
            Assert.Null(Run("items[10]", vars));
        }

        [Fact]
        public void Strings_HaveEscapes()
        {
            Assert.Equal("it's \"ok\"", Run(@"'it\'s ""ok""'"));
        }

        [Fact]
        public void SyntaxErrors_SayWhere()
        {
            Assert.Equal("Unexpected end of expression (at 4)", Eval.CheckExpression("1 + "));
            Assert.Contains("did you mean '=='", Eval.CheckExpression("a = 1"));
            Assert.Equal("Unexpected end of expression (at 11)", Eval.CheckTemplate("Hello @(1 +)"));
            Assert.Null(Eval.CheckTemplate("Hello @name"));
        }

        [Fact]
        public void Template_EndsAPathBeforeAFullStop_AndKeepsLoneAts()
        {
            var vars = Vars(("name", "Alice"), ("user", new Dictionary<string, object?> { ["name"] = "Bob" }));
            Assert.Equal("Hi Alice.", Render("Hi @name.", vars));
            Assert.Equal("Hi Bob, see mail@ example.com", Render("Hi @user.name, see mail@ example.com", vars));
        }

        [Fact]
        public void Template_FormatsWithoutCulture()
        {
            var vars = Vars(("pi", 3.5), ("items", new List<object?> { 1L, "two" }), ("whole", 2.0));
            Assert.Equal("3.5 [1,\"two\"] 2", Render("@pi @items @whole", vars));
        }

        [Fact]
        public void Value_KeepsTheTypeOfASingleHole()
        {
            var items = new List<object?> { 1L };
            var vars = Vars(("items", items));

            Assert.Same(items, Eval.Value("@items", vars).Value);
            Assert.Equal(2d, Eval.Value("@(1 + 1)", vars).Value);
            Assert.Equal("n: 2", Eval.Value("n: @(1 + 1)", vars).Value);
        }

        [Fact]
        public void Set_CreatesTheStructureOnTheWay()
        {
            var vars = new Dictionary<string, object>();
            Eval.Set(vars, "a.b[2].c", 5L);

            Assert.Equal(5L, Eval.Resolve(vars, "a.b[2].c"));
            Assert.Equal(3, ((List<object?>)Eval.Resolve(vars, "a.b")!).Count);
        }

        [Fact]
        public void CompiledExpressions_AreSafeToShareAcrossThreads()
        {
            Parallel.For(0, 2000, i =>
            {
                var vars = Vars(("x", (long)i));
                Assert.Equal((double)i * 2, Eval.TypedExecute<double>("x * 2", vars));
            });
        }
    }
}
