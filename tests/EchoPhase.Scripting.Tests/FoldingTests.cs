// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Scripting.Engine;

namespace EchoPhase.Scripting.Tests
{
    /// <summary>Constant expressions computed when parsed: same answers, and only where that is safe.</summary>
    public class FoldingTests
    {
        private static Node Parse(string source) => ExpressionParser.Parse(source);

        [Theory]
        [InlineData("60 * 60 * 24", 86400d)]
        [InlineData("-1", -1d)]
        [InlineData("upper('ab') + 'c'", "ABc")]
        [InlineData("len([1, 2, 3])", 3d)]
        [InlineData("[10, 20, 30][1]", 20d)]
        [InlineData("{ a: { b: 5 } }.a.b", 5d)]
        [InlineData("sha256('abc')[:8]", "ba7816bf")]
        [InlineData("2 in [1, 2]", true)]
        [InlineData("true ? 1 : 2", 1d)]
        public void ConstantExpressions_BecomeLiterals(string source, object expected)
        {
            var node = Parse(source);
            var literal = Assert.IsType<Literal>(node);
            Assert.Equal(expected, literal.Value);
        }

        [Theory]
        [InlineData("rand()")]
        [InlineData("uuid4()")]
        [InlineData("now()")]
        [InlineData("date('2026-01-01')")]          // reads the program's time zone
        [InlineData("x + 1")]
        [InlineData("map([1, 2], x => x)")]          // a lambda is not a constant
        [InlineData("1 / 0")]                        // fails: left to fail when run
        [InlineData("range(1000)")]                  // too large to keep in the cache
        [InlineData("[range(100)]")]                 // large inside
        [InlineData("len(range(1000000))")]          // too much work for the parser
        public void OtherExpressions_StayAsTheyAre(string source)
            => Assert.IsNotType<Literal>(Parse(source));

        [Fact]
        public void AConstantCondition_DropsTheBranchNotTaken()
        {
            // The branch not taken may even be one that fails.
            Assert.IsType<PathNode>(Parse("true ? x : 1 / 0"));
            Assert.Equal(5d, Eval.TypedExecute<double>("false ? 1 / 0 : x", new Dictionary<string, object> { ["x"] = 5d }));
        }

        [Fact]
        public void FailuresStillHappenWhenRun_NotWhenSaved()
        {
            Assert.Null(Eval.CheckExpression("x > 0 ? 1 / 0 : 1"));
            Assert.Throws<DivideByZeroException>(() => Eval.TypedExecute<object>("1 / 0", new Dictionary<string, object>()));
        }

        [Fact]
        public void FoldedCollections_AreShared_SoSetCopiesThem()
        {
            var vars = new Dictionary<string, object>();
            Eval.Set(vars, "a", Eval.Evaluate("@(merge({ x: [1] }, { y: 2 }))", vars));
            Eval.Set(vars, "a.x[0]", 9L);
            Assert.Equal("{\"x\":[9],\"y\":2}", Eval.TypedExecute<string>("a", vars));
            Assert.Equal("{\"x\":[1],\"y\":2}", Eval.TypedExecute<string>("merge({ x: [1] }, { y: 2 })", vars));
        }

        [Fact]
        public void FoldingDuringAnEvaluation_DoesNotSpendItsBudget()
        {
            // A cache miss parses (and folds) inside an evaluation; its budget is untouched.
            var vars = new Dictionary<string, object>();
            Assert.Equal(999_000d, Eval.TypedExecute<double>("len(range(999000)) + 0 * len(range(5000))", vars));
        }
    }
}
