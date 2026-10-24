// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Profilers.Extensions;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Observers;
using EchoPhase.Runners.Blocks.Validation;
using EchoPhase.Runners.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Tests
{
    /// <summary>
    /// Builds the runner as the app does. No Discord/Twitch clients are
    /// registered: their blocks fail to resolve, which is how the tests get a
    /// failing block.
    /// </summary>
    internal sealed class RunnerHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        public RunnerHarness(params IBlockRunObserver[] observers)
            : this(_ => { }, observers)
        {
        }

        /// <summary><paramref name="configure"/> adds or replaces services (fakes for the host's).</summary>
        public RunnerHarness(Action<IServiceCollection> configure, params IBlockRunObserver[] observers)
        {
            var services = new ServiceCollection()
                .AddProfiler()
                .AddRunners();
            foreach (var observer in observers)
                services.AddSingleton(observer);
            configure(services);
            _provider = services.BuildServiceProvider();
        }

        public Guid UserId { get; } = Guid.NewGuid();

        public BlockProgramValidator Validator => _provider.GetRequiredService<BlockProgramValidator>();

        public BlockTypeRegistry Registry => _provider.GetRequiredService<BlockTypeRegistry>();

        public static BlockProgram Parse(string json)
            => JsonSerializer.Deserialize<BlockProgram>(json, BlockParamsBinder.Options)!;

        public async Task<IBlockExecutionContext> RunAsync(
            string programJson, BlockLimits? limits = null, object? input = null, CancellationToken? ct = null,
            WebhookReply? reply = null, string? timeZone = null, Guid? workflowId = null)
        {
            var program = Parse(programJson);
            var issues = Validator.Validate(program, maxBlocks: 200);
            Assert.True(issues.Count == 0, string.Join(" | ", issues.Select(i => $"{i.BlockId}: {i.Message}")));

            program.Settings.TimeZone = timeZone;
            var context = new BlockExecutionContext(program, _provider, UserId,
                ct ?? TestContext.Current.CancellationToken)
            {
                Limits = limits ?? new BlockLimits(1_000, 100),
                Reply = reply,
                WorkflowId = workflowId
            };

            return await _provider.GetRequiredService<BlocksRunner>()
                .RunAsync(context, Validator.ManualStartIds(program), input);
        }

        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }
}
