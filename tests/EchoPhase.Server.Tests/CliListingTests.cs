// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Cli.Commands;
using EchoPhase.Cli.Commands.Workflows;
using EchoPhase.Server.Services.Workflows.Commands;

namespace EchoPhase.Server.Tests
{
    public class CliListingTests
    {
        private static readonly List<int> Rows = Enumerable.Range(1, 1234).ToList();

        // Rows after the cursor, as a keyset query would return them.
        private static Task<List<int>> Fetch(string? after, int take, CancellationToken _)
        {
            var from = after is null ? 0 : int.Parse(after);
            return Task.FromResult(Rows.Where(r => r > from).Take(take).ToList());
        }

        private static async Task<(List<int> Seen, string? Next, int Pages)> Run(ListingSettings settings)
        {
            var seen = new List<int>();
            var pages = 0;
            var next = await Listing.PagesAsync(settings, Fetch, r => r.ToString(),
                page => { pages++; seen.AddRange(page); return Task.CompletedTask; },
                CancellationToken.None);
            return (seen, next, pages);
        }

        [Fact]
        public async Task OnePageAndTheCursorOfTheNext()
        {
            var (seen, next, pages) = await Run(new ListingSettings { Limit = 50 });
            Assert.Equal(Enumerable.Range(1, 50), seen);
            Assert.Equal("50", next);
            Assert.Equal(1, pages);
        }

        [Fact]
        public async Task TheCursorContinuesWhereThePageEnded()
        {
            var (seen, next, _) = await Run(new ListingSettings { Limit = 50, After = "1200" });
            Assert.Equal(Enumerable.Range(1201, 34), seen);
            Assert.Null(next);
        }

        [Fact]
        public async Task AllStreamsEveryRowInBatches()
        {
            var (seen, next, pages) = await Run(new ListingSettings { All = true, Json = true });
            Assert.Equal(Rows, seen);
            Assert.Null(next);
            Assert.Equal(3, pages);
        }

        [Fact]
        public async Task AnExactlyFullPageHasNoNext()
        {
            var (seen, next, _) = await Run(new ListingSettings { Limit = 34, After = "1200" });
            Assert.Equal(34, seen.Count);
            Assert.Null(next);
        }

        [Theory]
        [InlineData(0, false, false, null)]
        [InlineData(1001, false, false, null)]
        [InlineData(50, true, false, null)]
        [InlineData(50, true, true, "x")]
        public void InvalidOptions(int limit, bool all, bool json, string? after)
            => Assert.False(new ListingSettings { Limit = limit, All = all, Json = json, After = after }.Validate().Successful);

        [Fact]
        public void AllWithJsonIsValid()
            => Assert.True(new ListingSettings { All = true, Json = true }.Validate().Successful);

        [Fact]
        public void LikeWildcardsAreLiteral()
            => Assert.Equal(@"%50\%\_off\\%", Listing.Contains(@"50%_off\"));

        [Fact]
        public void TimeCursorsRoundTripToTheTick()
        {
            var at = new DateTime(2026, 10, 24, 14, 22, 41, DateTimeKind.Utc).AddTicks(7151170);
            Assert.True(Cursors.TryParseTime(Cursors.Time(at), out var back));
            Assert.Equal(at, back);
            Assert.Equal(DateTimeKind.Utc, back.Kind);
            Assert.False(Cursors.TryParseTime("not a cursor", out _));
        }

        [Fact]
        public void WorkflowFileReadsAnExport()
        {
            var (name, program) = WorkflowFile.Parse(
                """{"name":"Nightly","program":{"blocks":[{"id":1,"type":"ScheduleTrigger","params":{"cron":"0 3 * * *"}}],"connections":[]}}""");
            Assert.Equal("Nightly", name);
            Assert.Single(program.Blocks);
        }

        [Theory]
        [InlineData("""{"program":{}}""")]
        [InlineData("""{"name":"x"}""")]
        [InlineData("""{"name":1,"program":{}}""")]
        [InlineData("""[]""")]
        public void WorkflowFileRejectsOtherShapes(string json)
            => Assert.Throws<FormatException>(() => WorkflowFile.Parse(json));
    }
}
