// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tests
{
    /// <summary>
    /// Inputs that would overflow the stack must fail as errors: an overflow
    /// cannot be caught and ends the whole server process. (If a guard broke,
    /// these tests would crash the test host rather than fail.)
    /// </summary>
    public class StackSafetyTests
    {
        [Theory]
        [InlineData("(", ")")]
        [InlineData("[", "]")]
        [InlineData("-", "")]
        [InlineData("!", "")]
        [InlineData("f(", ")")]
        [InlineData("x => ", "")]
        public void DeeplyNestedSource_IsASyntaxError(string open, string close)
        {
            const int Depth = 100_000;
            var source = string.Concat(Enumerable.Repeat(open, Depth)) + "1" + string.Concat(Enumerable.Repeat(close, Depth));

            Assert.Contains("Nested more than", Eval.CheckExpression(source));
            Assert.Contains("Nested more than", Eval.CheckTemplate("@(" + source + ")"));
        }

        [Fact]
        public void ReasonableNesting_StillParses()
            => Assert.Equal(1d, Eval.TypedExecute<double>(new string('(', 150) + "1" + new string(')', 150), new Dictionary<string, object>()));

        [Fact]
        public void ALambdaPassedToItself_StopsWithAnError()
        {
            var vars = new Dictionary<string, object>();
            Eval.Set(vars, "f", Eval.Evaluate("@(x => map([1], f))", vars));

            var ex = Assert.Throws<InvalidOperationException>(() => Eval.TypedExecute<object>("map([1], f)", vars));
            Assert.Contains("more than", ex.Message);

            // The depth is per evaluation: the next one starts from zero.
            Assert.Equal("[2]", Eval.TypedExecute<string>("map([1], x => x * 2)", vars));
        }
    }
}
