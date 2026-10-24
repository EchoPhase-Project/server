// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tests
{
    /// <summary>Slices, comprehensions, in, strip, slice() and splice(), as in Python (and JavaScript for splice).</summary>
    public class PythonicTests
    {
        private static readonly Dictionary<string, object> Vars = new()
        {
            ["xs"] = new List<object?> { 0L, 1L, 2L, 3L, 4L, 5L },
            ["s"] = "hello",
            ["users"] = new List<object?>
            {
                new Dictionary<string, object?> { ["name"] = "Ann", ["age"] = 31L },
                new Dictionary<string, object?> { ["name"] = "Bob", ["age"] = 17L },
            },
            ["for"] = "still a variable",
        };

        private static string Text(string expression) => Eval.TypedExecute<string>(expression, Vars);

        [Theory]
        [InlineData("xs[1:3]", "[1,2]")]
        [InlineData("xs[:2]", "[0,1]")]
        [InlineData("xs[4:]", "[4,5]")]
        [InlineData("xs[-2:]", "[4,5]")]
        [InlineData("xs[:-4]", "[0,1]")]
        [InlineData("xs[::2]", "[0,2,4]")]
        [InlineData("xs[::-1]", "[5,4,3,2,1,0]")]
        [InlineData("xs[4:1:-1]", "[4,3,2]")]
        [InlineData("xs[-1:-4:-2]", "[5,3]")]
        [InlineData("xs[10:20]", "[]")]
        [InlineData("xs[:]", "[0,1,2,3,4,5]")]
        [InlineData("s[1:4]", "ell")]
        [InlineData("s[::-1]", "olleh")]
        [InlineData("split('a,b,c', ',')[1:]", "[\"b\",\"c\"]")]
        [InlineData("xs[len(xs) - 2:]", "[4,5]")]
        [InlineData("xs[true ? 1 : 2]", "1")]
        [InlineData("[x for x in range(10)][len([x for x in range(10)]) - 3::-1]", "[7,6,5,4,3,2,1,0]")]
        [InlineData("[x for x in range(10)][:len([x for x in range(10)]) - 3:-1]", "[9,8]")]
        [InlineData("xs[len(xs) - 3::-1]", "[3,2,1,0]")]
        [InlineData("xs[:len(xs) - 3:-1]", "[5,4]")]
        public void Slices_WorkLikePython(string expression, string expected)
            => Assert.Equal(expected, Text(expression));

        [Fact]
        public void Slices_RejectZeroStep_AndAssignment()
        {
            Assert.Throws<InvalidOperationException>(() => Text("xs[::0]"));
            Assert.NotNull(Eval.CheckPath("xs[1:2]"));
            Assert.Equal("ll", Eval.TypedProcess<string>("@s[2:4]", Vars));
        }

        [Theory]
        [InlineData("[x * 2 for x in xs if x % 2 == 0]", "[0,4,8]")]
        [InlineData("[u.name for u in users if u.age >= 18]", "[\"Ann\"]")]
        [InlineData("[a + b for a in ['x', 'y'] for b in ['1', '2']]", "[\"x1\",\"x2\",\"y1\",\"y2\"]")]
        [InlineData("[i + ':' + c for i, c in enumerate(s[:2])]", "[\"0:h\",\"1:e\"]")]
        [InlineData("[k + '=' + v for k, v in items({ a: 1, b: 2 })]", "[\"a=1\",\"b=2\"]")]
        [InlineData("[k for k in { a: 1, b: 2 }]", "[\"a\",\"b\"]")]
        [InlineData("[c for c in 'abc' if c != 'b']", "[\"a\",\"c\"]")]
        [InlineData("[x for x in xs if x in [1, 3] if x > 1]", "[3]")]
        [InlineData("[[x, y] for x, y in zip([1, 2], ['a', 'b', 'c'])]", "[[1,\"a\"],[2,\"b\"]]")]
        [InlineData("[x for x in xs][-1]", "5")]
        [InlineData("[for]", "[\"still a variable\"]")]
        public void Comprehensions_WorkLikePython(string expression, string expected)
            => Assert.Equal(expected, Text(expression));

        [Fact]
        public void Comprehensions_ReportBadShapes()
        {
            Assert.NotNull(Eval.CheckExpression("[x for x xs]"));
            Assert.NotNull(Eval.CheckExpression("[x for x, x in xs]"));
            Assert.Throws<InvalidOperationException>(() => Text("[a for a, b in xs]"));
            Assert.Throws<InvalidOperationException>(() => Text("[x for x in 5]"));
        }

        [Theory]
        [InlineData("2 in xs", "true")]
        [InlineData("9 not in xs", "true")]
        [InlineData("'ell' in s", "true")]
        [InlineData("'a' in { a: 1 }", "true")]
        [InlineData("1 + 1 in xs && 'z' not in s", "true")]
        [InlineData("not 2 in xs", "false")]
        public void In_TestsMembership(string expression, string expected)
            => Assert.Equal(expected, Text(expression));

        [Theory]
        [InlineData("strip('  hi \\n')", "hi")]
        [InlineData("lstrip('  hi  ')", "hi  ")]
        [InlineData("rstrip('  hi  ')", "  hi")]
        [InlineData("strip('xxhixy', 'xy')", "hi")]
        [InlineData("lstrip('0042', '0')", "42")]
        [InlineData("rstrip('1.500', '0')", "1.5")]
        public void Strip_TakesWhitespaceOrGivenCharacters(string expression, string expected)
            => Assert.Equal(expected, Text(expression));

        [Theory]
        [InlineData("slice(xs, 1, 3)", "[1,2]")]
        [InlineData("slice(xs, -2)", "[4,5]")]
        [InlineData("slice(xs, null, null, -2)", "[5,3,1]")]
        [InlineData("slice(s, 1)", "ello")]
        [InlineData("splice(xs, 1, 2)", "[0,3,4,5]")]
        [InlineData("splice(xs, 1, 0, 'a', 'b')", "[0,\"a\",\"b\",1,2,3,4,5]")]
        [InlineData("splice(xs, -1, 1, 9)", "[0,1,2,3,4,9]")]
        [InlineData("splice(xs, 4)", "[0,1,2,3]")]
        public void SliceAndSplice_ReturnNewValues(string expression, string expected)
        {
            Assert.Equal(expected, Text(expression));
            Assert.Equal("[0,1,2,3,4,5]", Text("xs"));
        }
    }
}
