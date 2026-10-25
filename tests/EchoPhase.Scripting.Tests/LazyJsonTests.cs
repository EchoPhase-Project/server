// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using System.Text.Json;

namespace EchoPhase.Scripting.Tests
{
    /// <summary>JSON read lazily: the same values as when read at once, for a fraction of the work.</summary>
    public class LazyJsonTests
    {
        private const string Document = """
            { "items": [ { "id": 1, "tags": ["a", "b"] }, { "id": 2.5, "tags": [] } ],
              "name": "shop", "empty": {}, "none": null, "ok": true, "count": 2 }
            """;

        private static Dictionary<string, object?> With(bool lazy)
        {
            using var document = JsonDocument.Parse(Document);
            var root = document.RootElement.Clone();
            return Body(lazy ? Eval.FromJsonLazily(root) : Eval.Plain(root));
        }

        // The document is an object, so its value is never null.
        private static Dictionary<string, object?> Body(object? body)
        {
            Assert.NotNull(body);
            return new() { ["body"] = body };
        }

        [Theory]
        [InlineData("body.items[0].id")]
        [InlineData("body.items[-1].id")]
        [InlineData("body.items[1].tags")]
        [InlineData("body.items[9]")]
        [InlineData("body.name + '!'")]
        [InlineData("body.none")]
        [InlineData("body.missing.deeper")]
        [InlineData("len(body.items)")]
        [InlineData("len(body)")]
        [InlineData("body.empty ? 'y' : 'n'")]
        [InlineData("body.items[1].tags ? 'y' : 'n'")]
        [InlineData("[t for i in body.items for t in i.tags]")]
        [InlineData("map(sort(body.items, i => i.id), i => i.id)")]
        [InlineData("sum(body.items, i => i.id)")]
        [InlineData("keys(body)")]
        [InlineData("merge(body.items[0], { extra: 1 })")]
        [InlineData("'name' in body")]
        [InlineData("'b' in body.items[0].tags")]
        [InlineData("body.items[0] == body.items[0]")]
        [InlineData("body['name']")]
        [InlineData("body.ok && body.count > 1")]
        public void LazyValues_ReadLikeOnesConvertedAtOnce(string expression)
            => Assert.Equal(
                Eval.TypedExecute<string>(expression, With(lazy: false)),
                Eval.TypedExecute<string>(expression, With(lazy: true)));

        [Fact]
        public void Serialized_TheyWriteTheJsonAsItCame()
        {
            var vars = With(lazy: true);
            Assert.Equal(JsonSerializer.Serialize(JsonDocument.Parse(Document).RootElement), Eval.TypedExecute<string>("json(body)", vars));
            var item = Eval.Evaluate("@body.items[1]", vars);
            Assert.NotNull(item);
            Assert.Equal("""{"id":2.5,"tags":[]}""", JsonSerializer.Serialize(item));
        }

        [Fact]
        public void Set_CopiesThemFirst()
        {
            var vars = With(lazy: true);
            Eval.Set(vars, "copy", Eval.Evaluate("@body.items[0]", vars));
            Eval.Set(vars, "copy.id", 99L);
            Eval.Set(vars, "copy.tags[0]", "z");
            Eval.Set(vars, "body.name", "changed");

            Assert.Equal("""{"id":99,"tags":["z","b"]}""", Eval.TypedExecute<string>("copy", vars));
            Assert.Equal("1 a", Eval.TypedExecute<string>("str(body.items[0].id) + ' ' + body.items[0].tags[0]", vars));
            Assert.Equal("changed", Eval.TypedExecute<string>("body.name", vars));
        }

        [Fact]
        public void ReadingOneField_CostsAFractionOfConvertingTheWhole()
        {
            var json = new StringBuilder("{\"items\":[");
            for (int i = 0; i < 20_000; i++)
                json.Append(i == 0 ? "" : ",").Append($"{{\"id\":{i},\"name\":\"item {i}\",\"price\":{i * 1.5},\"tags\":[\"x\",\"y\"]}}");
            json.Append("]}");
            using var document = JsonDocument.Parse(json.ToString());
            var root = document.RootElement.Clone();

            static long Allocated(Func<object?> run)
            {
                run();
                long before = GC.GetAllocatedBytesForCurrentThread();
                run();
                return GC.GetAllocatedBytesForCurrentThread() - before;
            }

            long whole = Allocated(() => Eval.EvaluateExpression("body.items[3].name", Body(Eval.Plain(root))));
            long lazy = Allocated(() => Eval.EvaluateExpression("body.items[3].name", Body(Eval.FromJsonLazily(root))));

            Assert.True(lazy * 5 < whole, $"lazy {lazy} B, at once {whole} B");
        }
    }
}
