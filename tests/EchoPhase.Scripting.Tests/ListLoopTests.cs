// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tests
{
    /// <summary>The list functions rewritten as loops, at their edges.</summary>
    public class ListLoopTests
    {
        private static string Text(string expression)
            => Eval.TypedExecute<string>(expression, new Dictionary<string, object>());

        [Theory]
        [InlineData("unique([3, 1, '3', 2, 1])", "[3,1,2]")]
        [InlineData("unique(['ab', 'Ab', 'b'], s => lower(s))", "[\"ab\",\"b\"]")]
        [InlineData("groupBy([1, 2, 3, 4, 5], x => x % 2 == 0 ? 'even' : 'odd')", "{\"odd\":[1,3,5],\"even\":[2,4]}")]
        [InlineData("reduce([], (a, x) => a + x, 7)", "7")]
        [InlineData("reduce(['a', 'b'], (a, x, i) => a + x + i, '')", "a0b1")]
        [InlineData("first([])", "")]
        [InlineData("first([4, 5, 6], x => x > 4)", "5")]
        [InlineData("last([4, 5, 6], x => x < 6)", "5")]
        [InlineData("last([4, 5, 6])", "6")]
        [InlineData("take([1, 2, 3], 5)", "[1,2,3]")]
        [InlineData("take([1, 2, 3], -1)", "[]")]
        [InlineData("skip([1, 2, 3], 2)", "[3]")]
        [InlineData("skip([1, 2, 3], 9)", "[]")]
        [InlineData("reverse([1, [2], 3])", "[3,[2],1]")]
        [InlineData("reverse('abc')", "cba")]
        [InlineData("flatten([[1, [2]], [], 3])", "[1,[2],3]")]
        [InlineData("enumerate(['a'], 5)", "[[5,\"a\"]]")]
        [InlineData("zip([1, 2, 3], ['a', 'b'], [true, false, true])", "[[1,\"a\",true],[2,\"b\",false]]")]
        [InlineData("items({ a: 1, b: [2] })", "[[\"a\",1],[\"b\",[2]]]")]
        [InlineData("entries({ a: 1 })", "[{\"key\":\"a\",\"value\":1}]")]
        [InlineData("fromEntries([{ key: 'a', value: 1 }, { key: 'a', value: 2 }])", "{\"a\":2}")]
        [InlineData("join([1, 'x', null, true], '-')", "1-x--true")]
        [InlineData("join([])", "")]
        [InlineData("toSet(['a', 1])", "{\"a\":true,\"1\":true}")]
        [InlineData("first('xyz')", "x")]
        public void Loops_KeepTheirResults(string expression, string expected)
            => Assert.Equal(expected, Text(expression));
    }
}
