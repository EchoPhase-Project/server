// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tests
{
    /// <summary>xxhash3, sha256, md5, base64 and Bloom filters.</summary>
    public class HashingTests
    {
        private static object? Run(string expression, Dictionary<string, object>? vars = null)
            => Eval.TypedExecute<object>(expression, vars ?? []);

        // A value the test stores as a variable: it must exist.
        private static object Value(string expression, Dictionary<string, object>? vars = null)
        {
            var value = Run(expression, vars);
            Assert.NotNull(value);
            return value;
        }

        private static string Text(string expression, Dictionary<string, object>? vars = null)
            => Eval.TypedExecute<string>(expression, vars ?? []);

        [Theory]
        [InlineData("sha256('abc')", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
        [InlineData("md5('abc')", "900150983cd24fb0d6963f7d28e17f72")]
        [InlineData("xxhash3('')", "2d06800538d394c2")]
        [InlineData("base64('hello')", "aGVsbG8=")]
        [InlineData("fromBase64('aGVsbG8=')", "hello")]
        [InlineData("fromBase64('aGVsbG8')", "hello")]
        [InlineData("fromBase64(base64('привет, мир'))", "привет, мир")]
        public void Hashes_MatchKnownValues(string expression, string expected)
            => Assert.Equal(expected, Text(expression));

        [Fact]
        public void Hashes_TreatEqualValuesAlike()
        {
            Assert.Equal(Text("xxhash3('1')"), Text("xxhash3(1)"));
            Assert.NotEqual(Text("xxhash3('a')"), Text("xxhash3('a', 7)"));
            Assert.Equal(Text("sha256('{\"a\":1}')"), Text("sha256({ a: 1 })"));
        }

        [Fact]
        public void Bloom_HasNoFalseNegatives_AndAboutTheAskedFalsePositives()
        {
            var vars = new Dictionary<string, object>
            {
                ["seen"] = Enumerable.Range(0, 2000).Select(i => (object?)$"user-{i}").ToList(),
                ["other"] = Enumerable.Range(0, 20000).Select(i => (object?)$"guest-{i}").ToList(),
            };
            vars["f"] = Value("bloomFrom(seen, 0.01)", vars);

            Assert.Equal(2000d, Run("count(seen, x => bloomHas(f, x))", vars));
            var falsePositives = Eval.TypedExecute<double>("count(other, x => bloomHas(f, x))", vars);
            Assert.InRange(falsePositives / 20000, 0, 0.02);
        }

        [Fact]
        public void Bloom_IsAPlainValue_ThatSurvivesJson_AndIsNeverChangedInPlace()
        {
            var vars = new Dictionary<string, object> { ["empty"] = Value("bloom(100)") };
            vars["one"] = Value("bloomAdd(empty, 'a')", vars);
            vars["copy"] = Value("parseJson(json(bloomAdd(one, ['b', 'c'])))", vars);

            Assert.Equal(false, Run("bloomHas(empty, 'a')", vars));
            Assert.Equal(true, Run("bloomHas(one, 'a')", vars));
            Assert.Equal(false, Run("bloomHas(one, 'b')", vars));
            Assert.Equal(true, Run("bloomHas(copy, 'a') && bloomHas(copy, 'b') && bloomHas(copy, 'c')", vars));
            Assert.Equal("3", Text("copy.count", vars));
            Assert.Equal("bloom", Text("copy.type", vars));
        }

        [Theory]
        [InlineData("bloom(0)")]
        [InlineData("bloom(10, 1.5)")]
        [InlineData("bloom(10000000, 0.0001)")]
        [InlineData("bloomHas({ a: 1 }, 'x')")]
        [InlineData("bloomHas(merge(bloom(10), { data: 'AAAAAAAAAAA*' }), 'x')")]
        [InlineData("fromBase64('***')")]
        public void Bloom_AndBase64_RejectBadInput(string expression)
            => Assert.Throws<InvalidOperationException>(() => Run(expression));
    }
}
