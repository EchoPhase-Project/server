// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks;

namespace EchoPhase.Runners.Tests
{
    public class BlockProgramValidatorTests : IAsyncDisposable
    {
        private readonly RunnerHarness _harness = new();

        public ValueTask DisposeAsync() => _harness.DisposeAsync();

        private IReadOnlyList<string> Issues(string json)
            => _harness.Validator.Validate(RunnerHarness.Parse(json), maxBlocks: 200)
                .Select(i => i.Message).ToList();

        [Fact]
        public void SoundProgram_HasNoIssues()
        {
            Assert.Empty(Issues("""
                {
                  "blocks": [
                    { "id": 1, "type": "WebhookTrigger", "params": { "secret": "0123456789abcdef" } },
                    { "id": 2, "type": "If", "params": { "condition": "input.ok" } },
                    { "id": 3, "type": "Print", "params": { "text": "yes" } }
                  ],
                  "connections": [ { "from": 1, "to": 2 }, { "from": 2, "output": "true", "to": 3 } ],
                  "settings": { "timeZone": "Europe/Moscow", "timeoutSeconds": 10 }
                }
                """));
        }

        [Theory]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "Print", "params": { "text": "a" } }, { "id": 1, "type": "Print", "params": { "text": "b" } } ] }""", "used more than once")]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "Print", "params": { "text": "" } } ] }""", "Text cannot be empty")]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "Print", "version": 9, "params": { "text": "a" } } ] }""", "version 9")]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "If", "params": { "condition": "x" } }, { "id": 2, "type": "Print", "params": { "text": "a" } } ], "connections": [ { "from": 1, "output": "main", "to": 2 } ] }""", "no output \"main\"")]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "Print", "params": { "text": "a" } } ], "connections": [ { "from": 1, "output": "error", "to": 1 } ] }""", "no output \"error\"")]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "Print", "params": { "text": "a" } } ], "connections": [ { "from": 1, "to": 7 } ] }""", "does not exist")]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "Print", "params": { "text": "a" } }, { "id": 2, "type": "Print", "params": { "text": "b" } } ], "connections": [ { "from": 1, "to": 2 }, { "from": 2, "to": 1 } ] }""", "cycle")]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "Print", "params": { "text": "a" } }, { "id": 2, "type": "ManualTrigger", "params": {} } ], "connections": [ { "from": 1, "to": 2 } ] }""", "cannot have inputs")]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "ScheduleTrigger", "params": { "cron": "every day" } } ] }""", "cron")]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "Print", "retry": { "maxTries": 50 }, "params": { "text": "a" } } ] }""", "Retry")]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "Print", "params": { "text": "a" } } ], "settings": { "timeZone": "Mars/Olympus" } }""", "IANA")]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "If", "params": { "condition": "a = 1" } } ] }""", "condition: Single '='")]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "DiscordEventTrigger", "params": { "connection": "bot", "events": ["MESSAGE_EXPLODE"] } } ] }""", "Unknown events: MESSAGE_EXPLODE")]
        [InlineData("""{ "blocks": [ { "id": 1, "type": "Print", "params": { "text": "@(len(1, 2))" } } ] }""", "text: 'len' takes 1")]
        public void BrokenProgram_IsRejected(string json, string expected)
        {
            Assert.Contains(Issues(json), issue => issue.Contains(expected));
        }

        [Fact]
        public void ManualStart_PrefersManualTriggers_AndSkipsEventTriggers()
        {
            var program = RunnerHarness.Parse("""
                {
                  "blocks": [
                    { "id": 1, "type": "ScheduleTrigger", "params": { "cron": "0 9 * * *" } },
                    { "id": 2, "type": "Print", "params": { "text": "root" } },
                    { "id": 3, "type": "Print", "params": { "text": "after schedule" } }
                  ],
                  "connections": [ { "from": 1, "to": 3 } ]
                }
                """);

            Assert.Equal([2], _harness.Validator.ManualStartIds(program));
        }

        [Fact]
        public void Registry_DescribesEveryType()
        {
            var described = _harness.Registry.All.Select(d => d.Type).ToHashSet();
            Assert.All(Enum.GetValues<BlockTypes>(), type => Assert.Contains(type, described));
        }

        [Fact]
        public void Catalog_DescribesParamsAsJsonSchema()
        {
            var catalog = new Blocks.Handlers.BlockTypeCatalog(_harness.Registry);

            var @if = Assert.Single(catalog.Types, t => t.Type == BlockTypes.If);
            Assert.Equal(["true", "false"], @if.Outputs);
            Assert.NotNull(@if.ParamsSchema["properties"]?["condition"]);

            var webhook = Assert.Single(catalog.Types, t => t.Type == BlockTypes.WebhookTrigger);
            Assert.True(webhook.IsTrigger);
            Assert.Equal(Enum.GetValues<BlockTypes>().Length, catalog.Types.Count);
        }
    }
}
