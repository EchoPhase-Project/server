// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Storage;
using EchoPhase.Scripting;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Tests
{
    /// <summary>The Storage block against an in-memory store: values outlive the run.</summary>
    public class StorageBlockTests : IAsyncDisposable
    {
        private readonly MemoryStore _store = new();
        private readonly RunnerHarness _harness;
        private readonly Guid _workflowId = Guid.NewGuid();
        private readonly Guid _packageId = Guid.NewGuid();

        public StorageBlockTests()
        {
            _harness = new RunnerHarness(services => services.AddSingleton<IWorkflowStore>(_store));
        }

        public ValueTask DisposeAsync() => _harness.DisposeAsync();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private Task<Blocks.Contexts.IBlockExecutionContext> Run(string blocks, object? input = null, Guid? workflowId = null,
            Guid? packageId = null)
            => _harness.RunAsync($$"""{ "blocks": [ {{blocks}} ], "connections": [ { "from": 1, "to": 2 } ] }""",
                input: input, workflowId: workflowId ?? _workflowId, packageId: packageId ?? _packageId);

        private StorageSlot Mine => new(_harness.UserId, _packageId, _workflowId);

        [Fact]
        public async Task ABloomFilterRemembersAcrossRuns()
        {
            const string Add = """
                { "id": 1, "type": "Storage", "params": { "operation": "Update", "key": "seen",
                  "value": "@(bloomAdd(coalesce(value, bloom(1000)), input.id))" } },
                { "id": 2, "type": "Print", "params": { "text": "@(bloomHas(input, 'b'))" } }
                """;
            await Run(Add, new Dictionary<string, object?> { ["id"] = "a" });
            var second = await Run(Add, new Dictionary<string, object?> { ["id"] = "b" });
            Assert.Empty(second.Errors);
            Assert.Equal(["true"], second.Output);

            const string Check = """
                { "id": 1, "type": "Storage", "params": { "operation": "Get", "key": "seen" } },
                { "id": 2, "type": "Print", "params": { "text": "@(bloomHas(input, 'a')) @(bloomHas(input, 'zzz'))" } }
                """;
            Assert.Equal(["true false"], (await Run(Check)).Output);
        }

        [Fact]
        public async Task GetGivesTheDefaultWhenNothingIsStored()
        {
            var context = await Run("""
                { "id": 1, "type": "Storage", "params": { "operation": "Get", "key": "cursor", "default": 0 } },
                { "id": 2, "type": "Print", "params": { "text": "@(input + 1)" } }
                """);
            Assert.Equal(["1"], context.Output);
        }

        [Fact]
        public async Task KeysAreTemplatesAndScopesAreApart()
        {
            await Run("""
                { "id": 1, "type": "Storage", "params": { "operation": "Set", "key": "user:@input.id", "value": "@input.name" } },
                { "id": 2, "type": "Print", "params": { "text": "@input" } }
                """, new Dictionary<string, object?> { ["id"] = 7, ["name"] = "Ann" });
            Assert.Equal("Ann", (await _store.GetAsync(Mine, "user:7", Ct))?.Value);

            // Another workflow, and the package's shared scope, do not see it.
            var other = await Run("""
                { "id": 1, "type": "Storage", "params": { "operation": "Get", "key": "user:7", "default": "none" } },
                { "id": 2, "type": "Print", "params": { "text": "@input" } }
                """, workflowId: Guid.NewGuid());
            Assert.Equal(["none"], other.Output);
            var shared = await Run("""
                { "id": 1, "type": "Storage", "params": { "operation": "Get", "key": "user:7", "scope": "Package", "default": "none" } },
                { "id": 2, "type": "Print", "params": { "text": "@input" } }
                """);
            Assert.Equal(["none"], shared.Output);
        }

        [Fact]
        public async Task ThePackageScopeIsSharedByItsWorkflows_AndNotByOtherPackages()
        {
            await Run("""
                { "id": 1, "type": "Storage", "params": { "operation": "Set", "key": "k", "value": "shared", "scope": "Package" } },
                { "id": 2, "type": "Print", "params": { "text": "@input" } }
                """);

            const string Get = """
                { "id": 1, "type": "Storage", "params": { "operation": "Get", "key": "k", "scope": "Package", "default": "none" } },
                { "id": 2, "type": "Print", "params": { "text": "@input" } }
                """;
            Assert.Equal(["shared"], (await Run(Get, workflowId: Guid.NewGuid())).Output);
            Assert.Equal(["none"], (await Run(Get, workflowId: Guid.NewGuid(), packageId: Guid.NewGuid())).Output);
        }

        [Fact]
        public async Task ListAndDelete()
        {
            foreach (var key in new[] { "a:1", "a:2", "b:1" })
                await _store.SetAsync(Mine, key, 1, null, Ct);

            var listed = await Run("""
                { "id": 1, "type": "Storage", "params": { "operation": "List", "prefix": "a:" } },
                { "id": 2, "type": "Print", "params": { "text": "@(join(map(input, x => x.key), ','))" } }
                """);
            Assert.Equal(["a:1,a:2"], listed.Output);

            var deleted = await Run("""
                { "id": 1, "type": "Storage", "params": { "operation": "Delete", "key": "a:1" } },
                { "id": 2, "type": "Print", "params": { "text": "@input" } }
                """);
            Assert.Equal(["true"], deleted.Output);
            Assert.Null(await _store.GetAsync(Mine, "a:1", Ct));
        }

        [Fact]
        public async Task AnUnsavedProgramHasNoStorage()
        {
            var context = await _harness.RunAsync("""
                { "blocks": [ { "id": 1, "type": "Storage", "params": { "operation": "Get", "key": "x", "scope": "Package" } } ] }
                """);
            Assert.Contains("runs unsaved", Assert.Single(context.Errors).Message);
        }

        [Fact]
        public void SetAndUpdateNeedAValue()
        {
            var program = RunnerHarness.Parse("""
                { "blocks": [ { "id": 1, "type": "Storage", "params": { "operation": "Set", "key": "x" } } ] }
                """);
            Assert.Contains(_harness.Validator.Validate(program, 10), i => i.Message.Contains("needs a value"));
        }

        /// <summary>The store's contract in a dictionary (as JSON, like the real one).</summary>
        private sealed class MemoryStore : IWorkflowStore
        {
            private readonly Dictionary<(StorageSlot, string), string> _values = [];

            public Task<StorageItem?> GetAsync(StorageSlot slot, string key, CancellationToken ct)
                => Task.FromResult(_values.TryGetValue((slot, key), out var json)
                    ? new StorageItem(key, Read(json), json.Length, DateTime.UtcNow, null)
                    : null);

            public Task SetAsync(StorageSlot slot, string key, object? value, TimeSpan? ttl, CancellationToken ct)
            {
                _values[(slot, key)] = Eval.ToJson(value);
                return Task.CompletedTask;
            }

            public Task<object?> UpdateAsync(StorageSlot slot, string key, Func<object?, object?> update, TimeSpan? ttl, CancellationToken ct)
            {
                var value = update(_values.TryGetValue((slot, key), out var json) ? Read(json) : null);
                _values[(slot, key)] = Eval.ToJson(value);
                return Task.FromResult(value);
            }

            public Task<bool> DeleteAsync(StorageSlot slot, string key, CancellationToken ct)
                => Task.FromResult(_values.Remove((slot, key)));

            public Task<IReadOnlyList<StorageItem>> ListAsync(
                StorageSlot slot, string? prefix, string? after, int limit, bool values, CancellationToken ct)
                => Task.FromResult<IReadOnlyList<StorageItem>>(_values
                    .Where(kv => kv.Key.Item1 == slot && kv.Key.Item2.StartsWith(prefix ?? "", StringComparison.Ordinal))
                    .OrderBy(kv => kv.Key.Item2, StringComparer.Ordinal)
                    .Take(limit)
                    .Select(kv => new StorageItem(kv.Key.Item2, values ? Read(kv.Value) : null, kv.Value.Length, DateTime.UtcNow, null))
                    .ToList());

            private static object? Read(string json)
            {
                using var document = System.Text.Json.JsonDocument.Parse(json);
                return Eval.Plain(document.RootElement);
            }
        }
    }
}
