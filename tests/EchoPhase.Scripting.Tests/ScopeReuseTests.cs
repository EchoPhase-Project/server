// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tests
{
    /// <summary>Scopes rebound across calls instead of made anew must never be seen by more than one call.</summary>
    public class ScopeReuseTests
    {
        private static string Text(string expression, Dictionary<string, object>? vars = null)
            => Eval.TypedExecute<string>(expression, vars ?? new Dictionary<string, object>());

        [Theory]
        // Lambdas made inside a lambda or a comprehension keep their own x.
        [InlineData("[map([10], f)[0] for f in map([1, 2, 3], x => y => x + y)]", "[11,12,13]")]
        [InlineData("[map([10], f)[0] for f in [y => x + y for x in [1, 2, 3]]]", "[11,12,13]")]
        [InlineData("map(map([1, 2], x => (y => x * y)), f => map([3], f)[0])", "[3,6]")]
        // Nested comprehensions and lambdas see the outer value of the moment.
        [InlineData("[[x + y for y in [10, 20]] for x in [1, 2]]", "[[11,21],[12,22]]")]
        [InlineData("map([1, 2], x => map([10, 20], y => x + y))", "[[11,21],[12,22]]")]
        [InlineData("[[a, b, c] for a, b in [[1, 2], [3, 4]] for c in [a * 10]]", "[[1,2,10],[3,4,30]]")]
        // Values from a call are kept, not the scope they came from.
        [InlineData("map([1, 2, 3], x => [x, x * 2])", "[[1,2],[2,4],[3,6]]")]
        [InlineData("map(['a', 'b'], (s, i) => { s: s, i: i })", "[{\"s\":\"a\",\"i\":0},{\"s\":\"b\",\"i\":1}]")]
        public void EachCall_SeesItsOwnValues(string expression, string expected)
            => Assert.Equal(expected, Text(expression));

        [Theory]
        [InlineData("filter([null, 1], x => x == null)", "[null]")]
        [InlineData("map([null, 2], (x, i) => x == null ? i : x)", "[0,2]")]
        [InlineData("[x for x in [null, 2] if x == null]", "[null]")]
        [InlineData("map([null], x => map([1], y => x))", "[[null]]")]
        [InlineData("map([null], x => [y for y in [1] if x == null])", "[[1]]")]
        // A null parameter hides a variable of the same name; it does not show it through.
        [InlineData("map([null], n => n)", "[null]")]
        public void NullParameters_ReadAsNull(string expression, string expected)
            => Assert.Equal(expected, Text(expression, new Dictionary<string, object> { ["n"] = 5d }));

        [Fact]
        public void ALambdaReenteringItself_KeepsEachCallsValue()
        {
            // g(n) = g(n - 1) + 1, through a variable: the inner call must not overwrite the outer's x.
            var vars = new Dictionary<string, object>();
            Eval.Set(vars, "g", Eval.Evaluate("@(x => x > 0 ? sum(map([x - 1], g)) + x * 0 + 1 : 0)", vars));
            Assert.Equal("[5,7]", Text("map([5, 7], g)", vars));
            Assert.Equal("[0,1,2]", Text("map([0, 1, 2], g)", vars));
        }

        [Fact]
        public void AFailingCall_DoesNotPoisonTheNext()
        {
            var vars = new Dictionary<string, object> { ["xs"] = new List<object?> { 1d, "x", 3d } };
            Eval.Set(vars, "f", Eval.Evaluate("@(x => x * 2)", vars));
            Assert.Throws<InvalidOperationException>(() => Text("map(xs, f)", vars));
            Assert.Equal("[2,6]", Text("map([1, 3], f)", vars));
        }
    }
}
