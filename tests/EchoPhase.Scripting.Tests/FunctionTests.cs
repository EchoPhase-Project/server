// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tests
{
    /// <summary>Literals, lambdas and the list, text, JSON and date functions.</summary>
    public class FunctionTests
    {
        private static readonly Dictionary<string, object?> Users = new()
        {
            ["users"] = new List<object?>
            {
                new Dictionary<string, object?> { ["name"] = "Ann", ["age"] = 31L, ["team"] = "a" },
                new Dictionary<string, object?> { ["name"] = "Bob", ["age"] = 17L, ["team"] = "b" },
                new Dictionary<string, object?> { ["name"] = "Cid", ["age"] = 45L, ["team"] = "a" },
            }
        };

        private static object? Run(string expression, Dictionary<string, object?>? vars = null)
            => Eval.EvaluateExpression(expression, vars ?? []);

        private static string Text(string expression, Dictionary<string, object?>? vars = null)
            => Eval.TypedExecute<string>(expression, vars ?? []);

        [Fact]
        public void Literals_BuildListsAndObjects()
        {
            Assert.Equal("[1,2,\"x\"]", Text("[1, 2, 'x']"));
            Assert.Equal("{\"a\":1,\"b c\":[true]}", Text("{ a: 1, 'b c': [true], }"));
            Assert.Empty(Assert.IsAssignableFrom<List<object?>>(Run("[]")));
            Assert.Equal("2", Text("len({ a: 1, b: 2 })"));
        }

        [Fact]
        public void Lambdas_FilterMapAndFind()
        {
            Assert.Equal("[\"Ann\",\"Cid\"]", Text("map(filter(users, u => u.age >= 18), u => u.name)", Users));
            Assert.Equal("0:Ann,1:Bob,2:Cid", Text("join(map(users, (u, i) => i + ':' + u.name), ',')", Users));
            Assert.Equal("Bob", Text("find(users, u => u.age < 18).name", Users));
            Assert.Equal(-1d, Run("findIndex(users, u => u.age > 99)", Users));
            Assert.Equal(true, Run("some(users, u => u.team == 'b') && every(users, u => u.age > 10)", Users));
        }

        [Fact]
        public void Lambdas_SeeTheVariablesAround()
        {
            var vars = new Dictionary<string, object?>(Users) { ["min"] = 40L };
            Assert.Equal(1d, Run("count(users, u => u.age > min)", vars));
        }

        [Fact]
        public void Aggregates_AndSorting()
        {
            Assert.Equal(93d, Run("sum(users, u => u.age)", Users));
            Assert.Equal(31d, Run("avg(users, u => u.age)", Users));
            Assert.Null(Run("avg([])"));
            Assert.Equal("[\"Bob\",\"Ann\",\"Cid\"]", Text("map(sort(users, u => u.age), u => u.name)", Users));
            Assert.Equal("[3,2,1]", Text("sortDesc([2, 3, 1])"));
            Assert.Equal(6d, Run("reduce([1, 2, 3], (acc, x) => acc + x, 0)"));
            Assert.Equal(3d, Run("max([1, 3, 2])"));
        }

        [Fact]
        public void Sort_IsStable_TypesKeys_AndExplainsMixedLists()
        {
            var pairs = new Dictionary<string, object?>
            {
                ["xs"] = new List<object?>
            {
                new Dictionary<string, object?> { ["k"] = 2L, ["id"] = "a" },
                new Dictionary<string, object?> { ["k"] = 1L, ["id"] = "b" },
                new Dictionary<string, object?> { ["k"] = 2L, ["id"] = "c" },
                new Dictionary<string, object?> { ["k"] = null, ["id"] = "d" },
            }
            };
            Assert.Equal("[\"d\",\"b\",\"a\",\"c\"]", Text("map(sort(xs, x => x.k), x => x.id)", pairs));
            Assert.Equal("[\"a\",\"c\",\"b\",\"d\"]", Text("map(sortDesc(xs, x => x.k), x => x.id)", pairs));

            // Numbers by value (numeric text and bools too); text ordinally, as with <.
            Assert.Equal("[true,\"2\",10]", Text("sort([10, '2', true])"));
            Assert.Equal("[\"10\",\"9\"]", Text("sort(['9', '10'])"));

            var ex = Assert.Throws<InvalidOperationException>(() => Run("sort([1, 'a'])"));
            Assert.Contains("cannot order 'a'", ex.Message);
        }

        [Fact]
        public void Grouping_UniqueAndObjects()
        {
            Assert.Equal("{\"a\":[\"Ann\",\"Cid\"],\"b\":[\"Bob\"]}",
                Text("fromEntries(map(entries(groupBy(users, u => u.team)), e => { key: e.key, value: map(e.value, u => u.name) }))", Users));
            Assert.Equal("[1,2]", Text("unique([1, '1', 2, 1])"));
            Assert.Equal("[\"a\",\"b\"]", Text("keys(merge({ a: 1 }, { b: 2, a: 3 }))"));
            Assert.Equal(3d, Run("merge({ a: 1 }, { a: 3 }).a"));
            Assert.Equal("[1,2,3]", Text("flatten([[1, 2], 3])"));
            Assert.Equal("[0,2,4]", Text("range(0, 6, 2)"));
            Assert.Equal("[2,3]", Text("take(skip([1, 2, 3, 4], 1), 2)"));
        }

        [Fact]
        public void Strings_AndJson()
        {
            Assert.Equal("[\"a\",\"b\"]", Text("split('a,b', ',')"));
            Assert.Equal("x-y", Text("replace('x y', ' ', '-')"));
            Assert.Equal("lo", Text("substring('hello', -2)"));
            Assert.Equal("007", Text("padStart(7, 3, '0')"));
            Assert.Equal(true, Run("matches('abc-123', '^[a-z]+-\\\\d+$')"));
            Assert.Equal("123", Text("regexMatch('id=123', 'id=(\\\\d+)')[1]"));
            Assert.Equal(2d, Run("indexOf('hello', 'l')"));
            Assert.Equal(1d, Run("indexOf([3, 4], 4)"));
            Assert.Equal("{\"a\":[1,\"x\"]}", Text("json(parseJson('{\"a\": [1, \"x\"]}'))"));
        }

        [Fact]
        public void Dates_InTheProgramsZone()
        {
            var vars = new Dictionary<string, object?> { [Eval.TimeZoneVariable] = "Europe/Moscow" };
            Assert.Equal("2026-10-20T12:00:00+03:00", Text("date('2026-10-20T09:00:00Z')", vars));
            Assert.Equal("2026-10-20T09:00:00+03:00", Text("date('2026-10-20 09:00')", vars));
            Assert.Equal("2026-10-23T09:00:00+03:00", Text("dateAdd('2026-10-20 09:00', 3, 'days')", vars));
            Assert.Equal(-2d, Run("dateDiff('2026-10-20', '2026-10-22', 'days')", vars));
            Assert.Equal(2d, Run("dateDiff('2026-03-31', '2026-01-31', 'months')"));
            Assert.Equal(2d, Run("datePart('2026-10-20', 'weekday')"));
            Assert.Equal("20.10.2026", Text("formatDate('2026-10-20T23:30:00Z', 'dd.MM.yyyy', 'UTC')"));
            Assert.Equal("2026-10-19T00:00:00+00:00", Text("startOf('2026-10-21T15:00:00Z', 'week')"));
            Assert.Equal(0d, Run("toUnix(fromUnix(0))"));
        }

        [Fact]
        public void Dates_FollowDaylightSaving()
        {
            // Berlin leaves summer time on 2026-10-25: a day later is the same wall time.
            var berlin = new Dictionary<string, object?> { [Eval.TimeZoneVariable] = "Europe/Berlin" };
            Assert.Equal("2026-10-25T12:00:00+01:00", Text("dateAdd('2026-10-24T12:00:00+02:00', 1, 'day')", berlin));
        }

        [Fact]
        public void TheTimeZoneVariable_IsNotReachableFromPrograms()
        {
            Assert.NotNull(Eval.CheckExpression("$timeZone"));
        }

        [Fact]
        public void Lambdas_AreCheckedWhenParsed()
        {
            Assert.NotNull(Eval.CheckExpression("map(xs, (a, a) => a)"));
            Assert.NotNull(Eval.CheckExpression("map(xs, true => 1)"));
            Assert.Null(Eval.CheckExpression("map(xs, () => 1)"));
            Assert.Null(Eval.CheckExpression("(a + b) * 2"));
            Assert.Throws<InvalidOperationException>(() => Run("map([1], 5)"));
        }

        [Fact]
        public void HugeWork_StopsAtTheBudget()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Run("len(map(range(2000), x => range(2000)))"));
            Assert.Contains("operations", ex.Message);
        }
    }
}
