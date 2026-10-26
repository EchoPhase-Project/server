// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System.Text.Json;
using EchoPhase.Configuration.Runner;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.DAL.Scylla.Linq;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Runs;
using EchoPhase.Runners.Blocks.Validation;
using EchoPhase.Server.Services.Blocks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace EchoPhase.Server.Tests
{
    /// <summary>BlockRunService on ScyllaDB in memory: starting, listing, reading and cancelling runs.</summary>
    public class BlockRunServiceTests : IDisposable
    {
        private static readonly BlockTypeRegistry Types = new(typeof(BlockTypeAttribute).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<BlockTypeAttribute>() is not null));

        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
        private readonly ScyllaTestDb _scylla;
        private readonly RunnerOptions _options = new();
        private readonly BlockRunRegistry _registry = new();
        private readonly BlockRunQueue _queue;
        private readonly Guid _user = Guid.NewGuid();

        public BlockRunServiceTests()
        {
            _scylla = new ScyllaTestDb(_time);
            _options.Blocks.MaxActivePerUser = 2;
            _queue = new BlockRunQueue(Options.Create(_options));
        }

        public void Dispose() => _scylla.Dispose();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private BlockRunService Service() => new(_scylla.Context(), _queue, _registry, Options.Create(_options), _time,
            new BlockProgramValidator(Types), new NoWorkflows());

        private static BlockProgram Program() => JsonSerializer.Deserialize<BlockProgram>("""
            { "blocks": [ { "id": 1, "type": "ManualTrigger", "params": {} },
                          { "id": 2, "type": "Print", "params": { "text": "hi" } } ],
              "connections": [ { "from": 1, "to": 2 } ] }
            """, BlockParamsBinder.Options) ?? throw new InvalidOperationException("The test program did not parse.");

        private async Task<BlockRun> Start()
        {
            var result = await Service().StartAsync(_user, null, new BlockRunStart(Program()), Ct);
            Assert.True(result.Successful, result.Error.Message);
            return result.Ok();
        }

        [Fact]
        public async Task Start_StoresAQueuedRun_AndItsActiveMarker()
        {
            var run = await Start();

            using var db = _scylla.Context();
            var stored = Must.Present(await db.BlockRuns.Where(r => r.UserId == _user && r.RunId == run.RunId).FirstOrDefaultAsync(Ct));
            Assert.Equal(BlockRunStatus.Queued, stored.Status);
            Assert.Equal(2, stored.BlockCount);
            Assert.NotNull(await db.ActiveBlockRuns.Where(a => a.RunId == run.RunId).FirstOrDefaultAsync(Ct));
        }

        [Fact]
        public async Task AWorkflowsRuns_AreListedNewestFirst_APageAtATime_AndOnlyItsOwn()
        {
            _options.Blocks.MaxActivePerUser = 10;
            Guid workflow = Guid.NewGuid(), other = Guid.NewGuid();
            var runs = new List<BlockRun>();
            foreach (var id in new[] { workflow, other, workflow, workflow })
            {
                runs.Add((await Service().StartAsync(_user, null, new BlockRunStart(Program(), WorkflowId: id), Ct)).Ok());
                _time.Advance(TimeSpan.FromSeconds(1));
            }
            await Start(); // not a workflow's

            var first = (await Service().ListByWorkflowAsync(_user, workflow, 2, null, Ct)).Ok();
            Assert.Equal([runs[3].RunId, runs[2].RunId], first.Items.Select(r => r.RunId));
            Assert.NotNull(first.NextCursor);
            var second = (await Service().ListByWorkflowAsync(_user, workflow, 2, first.NextCursor, Ct)).Ok();
            Assert.Equal([runs[0].RunId], second.Items.Select(r => r.RunId));

            // Another user asking for the same workflow id sees nothing.
            Assert.Empty((await Service().ListByWorkflowAsync(Guid.NewGuid(), workflow, 10, null, Ct)).Ok().Items);
        }

        [Fact]
        public async Task Start_BeyondTheActiveLimit_IsRefused()
        {
            await Start();
            await Start();

            var third = await Service().StartAsync(_user, null, new BlockRunStart(Program()), Ct);

            Assert.Equal(BlockRunErrors.TooManyActive, third.Error.Code);
        }

        [Fact]
        public async Task List_IsNewestFirst_APageAtATime()
        {
            var first = await Start();
            _registry.Release(first.RunId);
            _time.Advance(TimeSpan.FromSeconds(1));
            var second = await Start();

            var page = (await Service().ListAsync(_user, 1, null, Ct)).Ok();
            var next = (await Service().ListAsync(_user, 1, page.NextCursor, Ct)).Ok();

            Assert.Equal(second.RunId, Assert.Single(page.Items).RunId);
            Assert.Equal(first.RunId, Assert.Single(next.Items).RunId);
            Assert.Equal(BlockRunErrors.Invalid, (await Service().ListAsync(_user, 1, "not base64!", Ct)).Error.Code);
        }

        [Fact]
        public async Task AnotherUsersRun_IsNotFound()
        {
            var run = await Start();

            Assert.Equal(BlockRunErrors.NotFound, (await Service().GetAsync(Guid.NewGuid(), run.RunId, Ct)).Error.Code);
            Assert.Equal(BlockRunErrors.NotFound, (await Service().CancelAsync(Guid.NewGuid(), Guid.NewGuid(), Ct)).Error.Code);
        }

        [Fact]
        public async Task Get_WithoutAResultYet_HasNoPayload()
        {
            var run = await Start();

            var details = (await Service().GetAsync(_user, run.RunId, Ct)).Ok();

            Assert.Equal(run.RunId, details.Run.RunId);
            Assert.Null(details.Result);
        }

        [Fact]
        public async Task Retry_OfAnUnfinishedRun_IsRefused()
        {
            var run = await Start();

            Assert.Equal(BlockRunErrors.NotFinished, (await Service().RetryAsync(_user, null, run.RunId, false, Ct)).Error.Code);
        }

        [Fact]
        public async Task Cancel_OfAQueuedRun_Succeeds()
        {
            var run = await Start();

            Assert.True((await Service().CancelAsync(_user, run.RunId, Ct)).Successful);
        }

        private sealed class NoWorkflows : IWorkflowSource
        {
            public Task<BlockProgram?> FindAsync(Guid userId, Guid workflowId, CancellationToken ct)
                => Task.FromResult<BlockProgram?>(null);
        }
    }
}
