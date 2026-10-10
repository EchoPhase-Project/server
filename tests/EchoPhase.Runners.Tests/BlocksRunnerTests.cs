// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Runners.Tests
{
    public class BlocksRunnerTests : IAsyncDisposable
    {
        private readonly RunnerHarness _harness = new();

        public ValueTask DisposeAsync() => _harness.DisposeAsync();

        private const string Forever = """
            {
              "blocks": [
                { "id": 1, "type": "Set", "params": { "name": "a", "value": 1 } },
                { "id": 2, "type": "While", "params": { "condition": "a == 1" } }
              ],
              "connections": [ { "from": 1, "to": 2 } ]
            }
            """;

        [Fact]
        public async Task SimpleProgram_Runs()
        {
            var context = await _harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "Set", "params": { "name": "a", "value": 1 } },
                    { "id": 2, "type": "Print", "params": { "text": "a = @a" } }
                  ],
                  "connections": [ { "from": 1, "to": 2 } ]
                }
                """);

            Assert.Empty(context.Errors);
            Assert.Equal(["a = 1"], context.Output);
            Assert.Equal(2, context.Steps);
        }

        [Fact]
        public async Task Input_IsThePreviousBlocksOutput_AndBlocksAreAddressableById()
        {
            var context = await _harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "ManualTrigger", "params": {} },
                    { "id": 2, "type": "Set", "params": { "name": "x", "value": "@(input.n + 1)" } },
                    { "id": 3, "type": "Print", "params": { "text": "@input / @blocks.b1.n" } }
                  ],
                  "connections": [ { "from": 1, "to": 2 }, { "from": 2, "to": 3 } ]
                }
                """, input: new Dictionary<string, object?> { ["n"] = 41L });

            Assert.Empty(context.Errors);
            Assert.Equal(["42 / 41"], context.Output);
        }

        [Fact]
        public async Task If_FollowsTheNamedOutput()
        {
            var context = await _harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "For", "params": { "counterVar": "j", "start": 1, "end": 3 } },
                    { "id": 2, "type": "If", "params": { "condition": "(j % 2) == 0" } },
                    { "id": 3, "type": "Print", "params": { "text": "even @j" } },
                    { "id": 4, "type": "Print", "params": { "text": "odd @j" } },
                    { "id": 5, "type": "Print", "params": { "text": "done after @input" } }
                  ],
                  "connections": [
                    { "from": 1, "output": "body", "to": 2 },
                    { "from": 2, "output": "true", "to": 3 },
                    { "from": 2, "output": "false", "to": 4 },
                    { "from": 1, "output": "done", "to": 5 }
                  ]
                }
                """);

            Assert.Empty(context.Errors);
            Assert.Equal(["odd 1", "even 2", "odd 3", "done after 3"], context.Output);
        }

        [Fact]
        public async Task Merge_WaitsForAllInputs()
        {
            var context = await _harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "Set", "params": { "name": "a", "value": [1, 2] } },
                    { "id": 2, "type": "Set", "params": { "name": "b", "value": 3 } },
                    { "id": 3, "type": "Merge", "params": { "mode": "Append" } },
                    { "id": 4, "type": "Print", "params": { "text": "@input" } }
                  ],
                  "connections": [ { "from": 1, "to": 3 }, { "from": 2, "to": 3 }, { "from": 3, "to": 4 } ]
                }
                """);

            Assert.Empty(context.Errors);
            Assert.Single(context.Output);
            var merged = Assert.IsType<List<object?>>(context.BlockOutputs["b3"]);
            Assert.Equal([1L, 2L, 3L], merged.Cast<long>());
        }

        [Fact]
        public async Task Merge_RunsWithWhatArrived_WhenABranchIsNotTaken()
        {
            var context = await _harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "Set", "params": { "name": "a", "value": 5 } },
                    { "id": 2, "type": "If", "params": { "condition": "a > 1" } },
                    { "id": 3, "type": "Set", "params": { "name": "big", "value": "yes" } },
                    { "id": 4, "type": "Set", "params": { "name": "small", "value": "yes" } },
                    { "id": 5, "type": "Merge", "params": { "mode": "BySource" } },
                    { "id": 6, "type": "Print", "params": { "text": "merged" } }
                  ],
                  "connections": [
                    { "from": 1, "to": 2 },
                    { "from": 2, "output": "true", "to": 3 },
                    { "from": 2, "output": "false", "to": 4 },
                    { "from": 3, "to": 5 }, { "from": 4, "to": 5 }, { "from": 5, "to": 6 }
                  ]
                }
                """);

            Assert.Empty(context.Errors);
            Assert.Equal(["merged"], context.Output);
            var merged = Assert.IsType<Dictionary<string, object?>>(context.BlockOutputs["b5"]);
            Assert.Equal(["b3"], merged.Keys);
        }

        [Fact]
        public async Task Disabled_PassesItsInputThrough()
        {
            var context = await _harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "Set", "params": { "name": "a", "value": 7 } },
                    { "id": 2, "type": "Print", "disabled": true, "params": { "text": "skipped" } },
                    { "id": 3, "type": "Print", "params": { "text": "got @input" } }
                  ],
                  "connections": [ { "from": 1, "to": 2 }, { "from": 2, "to": 3 } ]
                }
                """);

            Assert.Empty(context.Errors);
            Assert.Equal(["got 7"], context.Output);
        }

        [Fact]
        public async Task OnErrorContinue_GoesOnWithTheErrorAsOutput()
        {
            var context = await _harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "DiscordGetUserGuilds", "onError": "Continue", "params": { "connection": "bot" } },
                    { "id": 2, "type": "Print", "params": { "text": "failed: @(input.error.type)" } }
                  ],
                  "connections": [ { "from": 1, "to": 2 } ]
                }
                """);

            Assert.Empty(context.Errors);
            Assert.Equal(["failed: InvalidOperationException"], context.Output);
        }

        [Fact]
        public async Task OnErrorOutput_TakesTheErrorOutput()
        {
            var context = await _harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "DiscordGetUserGuilds", "onError": "ErrorOutput", "params": { "connection": "bot" } },
                    { "id": 2, "type": "Print", "params": { "text": "ok" } },
                    { "id": 3, "type": "Print", "params": { "text": "error path" } }
                  ],
                  "connections": [
                    { "from": 1, "output": "main", "to": 2 },
                    { "from": 1, "output": "error", "to": 3 }
                  ]
                }
                """);

            Assert.Empty(context.Errors);
            Assert.Equal(["error path"], context.Output);
        }

        [Fact]
        public async Task Stop_EndsTheWholeRun_FromInsideALoop()
        {
            var context = await _harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "For", "params": { "counterVar": "i", "start": 1, "end": 5 } },
                    { "id": 2, "type": "DiscordGetUserGuilds", "params": { "connection": "bot" } },
                    { "id": 3, "type": "Print", "params": { "text": "after" } }
                  ],
                  "connections": [
                    { "from": 1, "output": "body", "to": 2 },
                    { "from": 1, "output": "done", "to": 3 }
                  ]
                }
                """);

            var error = Assert.Single(context.Errors);
            Assert.Equal(2, error.Id);
            Assert.Empty(context.Output);
            Assert.Equal(0, context.Depth);
        }

        [Fact]
        public async Task Retry_TriesTheConfiguredNumberOfTimes()
        {
            var recorder = new Blocks.Observers.ExecutionRecorder();
            await using var harness = new RunnerHarness(recorder);

            var context = await harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "DiscordGetUserGuilds", "retry": { "maxTries": 3, "delayMs": 0 },
                      "params": { "connection": "bot" } }
                  ]
                }
                """);

            Assert.Single(context.Errors);
            var record = Assert.Single(recorder.Records);
            Assert.Equal(Blocks.Observers.BlockExecutionStatus.Failed, record.Status);
            Assert.Equal(3, record.Attempts);
        }

        [Fact]
        public async Task EndlessLoopWithEmptyBody_StopsAtStepLimit()
        {
            var context = await _harness.RunAsync(Forever, new BlockLimits(MaxSteps: 50, MaxOutputEntries: 100));

            var error = Assert.Single(context.Errors);
            Assert.Equal("LimitExceeded", error.Type);
            Assert.Equal(2, error.Id);
            Assert.Equal(51, context.Steps);
        }

        [Fact]
        public async Task PrintingLoop_StopsAtOutputLimit()
        {
            var context = await _harness.RunAsync("""
                {
                  "blocks": [
                    { "id": 1, "type": "Set", "params": { "name": "a", "value": 1 } },
                    { "id": 2, "type": "While", "params": { "condition": "a == 1" } },
                    { "id": 3, "type": "Print", "params": { "text": "tick" } }
                  ],
                  "connections": [ { "from": 1, "to": 2 }, { "from": 2, "output": "body", "to": 3 } ]
                }
                """, new BlockLimits(MaxSteps: 10_000, MaxOutputEntries: 5));

            var error = Assert.Single(context.Errors);
            Assert.Equal("LimitExceeded", error.Type);
            Assert.Contains("Output", error.Message);
            Assert.Equal(6, context.Output.Count);
        }

        [Fact]
        public async Task Cancellation_EndsTheRun()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _harness.RunAsync(Forever, ct: cts.Token));
        }
    }
}
