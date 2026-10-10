// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Observers;

namespace EchoPhase.Runners.Tests
{
    public class BlockRunObserverTests
    {
        private sealed class Recorder : IBlockRunObserver
        {
            public List<string> Events { get; } = [];

            public ValueTask OnBlockStartingAsync(BlockEvent e)
            {
                Events.Add($"start {e.Block.Id} d{e.Depth}");
                return ValueTask.CompletedTask;
            }

            public ValueTask OnBlockCompletedAsync(BlockCompletedEvent e)
            {
                Events.Add($"done {e.Block.Id} d{e.Depth} -> {e.Output ?? "end"}");
                return ValueTask.CompletedTask;
            }

            public ValueTask OnBlockFailedAsync(BlockFailedEvent e)
            {
                Events.Add($"fail {e.Block.Id} {e.Error.Type}{(e.Handled ? " handled" : "")}");
                return ValueTask.CompletedTask;
            }
        }

        private sealed class Throwing : IBlockRunObserver
        {
            public ValueTask OnBlockStartingAsync(BlockEvent e) => throw new InvalidOperationException("observer bug");
        }

        [Fact]
        public async Task SeesEveryBlockInOrder()
        {
            var recorder = new Recorder();
            await using var harness = new RunnerHarness(recorder);

            await harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "Set", "params": { "name": "a", "value": 1 } },
                    { "id": 2, "type": "Print", "params": { "text": "a = @a" } }
                  ],
                  "connections": [ { "from": 1, "to": 2 } ]
                }
                """);

            Assert.Equal(["start 1 d0", "done 1 d0 -> main", "start 2 d0", "done 2 d0 -> main"], recorder.Events);
        }

        [Fact]
        public async Task LoopBodies_RunOneLevelDeeper()
        {
            var recorder = new Recorder();
            await using var harness = new RunnerHarness(recorder);

            var context = await harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "For", "params": { "counterVar": "i", "start": 0, "end": 2 } },
                    { "id": 2, "type": "Print", "params": { "text": "i = @i" } }
                  ],
                  "connections": [ { "from": 1, "output": "body", "to": 2 } ]
                }
                """);

            Assert.Empty(context.Errors);
            Assert.Equal(3, context.Output.Count);
            Assert.Equal(3, recorder.Events.Count(e => e == "start 2 d1"));
            Assert.Equal("done 1 d0 -> done", recorder.Events[^1]);
        }

        [Fact]
        public async Task HandledFailure_IsReportedAsHandled()
        {
            var recorder = new Recorder();
            await using var harness = new RunnerHarness(recorder);

            await harness.RunAsync("""
                { "blocks": [ { "id": 1, "type": "DiscordGetUserGuilds", "onError": "Continue", "params": { "connection": "bot" } } ] }
                """);

            Assert.Contains(recorder.Events, e => e.StartsWith("fail 1 ") && e.EndsWith(" handled"));
            Assert.Equal("done 1 d0 -> main", recorder.Events[^1]);
        }

        [Fact]
        public async Task ThrowingObserver_DoesNotBreakTheRun()
        {
            var recorder = new Recorder();
            await using var harness = new RunnerHarness(recorder, new Throwing());

            var context = await harness.RunAsync("""
                { "blocks": [ { "id": 1, "type": "Print", "params": { "text": "still runs" } } ] }
                """);

            Assert.Empty(context.Errors);
            Assert.Equal(["still runs"], context.Output);
            Assert.Equal(2, recorder.Events.Count);
        }
    }
}
