// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tests
{
    /// <summary>
    /// Arithmetic and comparisons on the unboxed path give exactly what the
    /// general rules give: each case is checked against the same expression
    /// with its numbers coming from variables (which takes the general path).
    /// </summary>
    public class NumericPathTests
    {
        private static readonly Dictionary<string, object> Vars = new()
        {
            ["one"] = 1d,
            ["two"] = 2d,
            ["zero"] = 0d,
            ["nan"] = double.NaN,
            ["s2"] = "2",
            ["sx"] = "x",
            ["t"] = true,
            ["f"] = false,
            ["list"] = new List<object?> { 1L },
            ["obj"] = new Dictionary<string, object?>(),
        };

        private static string Outcome(string expression)
        {
            try
            {
                return Eval.TypedExecute<string>(expression, Vars);
            }
            catch (Exception ex)
            {
                return ex.GetType().Name + ": " + ex.Message;
            }
        }

        [Theory]
        // literal (unboxed) side vs the same value from a variable (general path)
        [InlineData("2 == s2", "two == s2")]
        [InlineData("2 != sx", "two != sx")]
        [InlineData("1 == t", "1 + zero == t")]
        [InlineData("0 == f", "zero == f")]
        [InlineData("2 == null", "two == null")]
        [InlineData("null != 2", "null != two")]
        [InlineData("1 == list", "1 + zero == list")]
        [InlineData("0 != obj", "zero != obj")]
        [InlineData("2 < s2", "two < s2")]
        [InlineData("2 < sx", "two < sx")]
        [InlineData("sx > 2", "sx > two")]
        [InlineData("2 > null", "two > null")]
        [InlineData("nan == 1", "nan == one")]
        [InlineData("nan < 1", "nan < one")]
        [InlineData("1 > nan", "one > nan")]
        [InlineData("-nan < 1", "-nan < one")]
        [InlineData("1 / 0", "1 / zero")]
        [InlineData("5 % 0", "5 % zero")]
        [InlineData("s2 * 3", "s2 * (two + 1)")]
        [InlineData("sx * 3", "sx * (two + 1)")]
        [InlineData("2 + 3 * 4 - 6 / 3", "two + 3 * 4 - 6 / 3")]
        [InlineData("-2 * -3", "-two * -3")]
        [InlineData("(true ? 1 : 2) + 0.5", "(t ? 1 : two) + 0.5")]
        [InlineData("1 + 'a'", "1 + zero + 'a'")]
        [InlineData("2 * 3 > 5 && 1 < 2", "two * 3 > 5 && 1 < two")]
        public void UnboxedPath_MatchesTheGeneralRules(string unboxed, string general)
            => Assert.Equal(Outcome(general), Outcome(unboxed));

        [Fact]
        public void Lambdas_SumWithoutBoxing_StillReadAnyValue()
        {
            var vars = new Dictionary<string, object>
            {
                ["xs"] = new List<object?> { 1L, "2", true, 3.5 },
            };
            Assert.Equal(7.5, Eval.TypedExecute<double>("sum(xs)", vars));
            Assert.Equal(15d, Eval.TypedExecute<double>("sum(xs, x => x * 2)", vars));
            Assert.Equal(0.90625, Eval.TypedExecute<double>("avg(xs, (x, i) => x * i / 4)", vars), 12);
            Assert.Null(Eval.TypedExecute<object>("avg([])", vars));
        }
    }
}
