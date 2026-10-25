// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System.Text.Json;
using EchoPhase.Configuration.Runner;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Runs;
using EchoPhase.Runners.Blocks.Validation;
using EchoPhase.Server.Services.Blocks;
using EchoPhase.Server.Services.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace EchoPhase.Server.Tests
{
    /// <summary>A program's hash: the same however it is written, different when it changes.</summary>
    public class BlockProgramHashTests
    {
        private static BlockProgram Parse(string json)
            => JsonSerializer.Deserialize<BlockProgram>(json, BlockParamsBinder.Options)
               ?? throw new InvalidOperationException("The test program did not parse.");

        [Fact]
        public void KeyOrderAndSpacing_DoNotChangeIt()
        {
            var a = Parse("""{ "blocks": [ { "id": 1, "type": "Print", "params": { "text": "hi" } } ], "connections": [] }""");
            var b = Parse("""{"connections":[],"blocks":[{"params":{"text":"hi"},"type":"Print","id":1}]}""");

            Assert.Equal(BlockProgramHash.Of(a), BlockProgramHash.Of(b));
            Assert.Matches("^[0-9a-f]{64}$", BlockProgramHash.Of(a));
        }

        [Fact]
        public void AnyChange_ChangesIt()
        {
            var a = Parse("""{ "blocks": [ { "id": 1, "type": "Print", "params": { "text": "hi" } } ] }""");
            var b = Parse("""{ "blocks": [ { "id": 1, "type": "Print", "params": { "text": "hi!" } } ] }""");

            Assert.NotEqual(BlockProgramHash.Of(a), BlockProgramHash.Of(b));
        }

        [Fact]
        public void StoredJson_HashesLikeTheProgram()
        {
            var program = Parse("""{ "blocks": [ { "id": 1, "type": "Print", "params": { "text": "hi" } } ] }""");

            Assert.Equal(BlockProgramHash.Of(program),
                BlockProgramHash.OfJson(JsonSerializer.Serialize(program, BlockParamsBinder.Options)));
        }
    }

    /// <summary>Workflow versions: by hash, linked to their parent, restores marked.</summary>
    public class WorkflowVersionTests : IDisposable
    {
        private static readonly BlockTypeRegistry Types = new(typeof(BlockTypeAttribute).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<BlockTypeAttribute>() is not null));

        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
        private readonly PostgresContext _db;
        private readonly ScyllaTestDb _scylla;
        private readonly RunnerOptions _options = new();
        private readonly User _owner = new("Ann") { UserName = "ann" };

        public WorkflowVersionTests()
        {
            _db = new PostgresContext(new DbContextOptionsBuilder<PostgresContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            _db.Users.Add(_owner);
            _db.SaveChanges();
            _scylla = new ScyllaTestDb(_time);
        }

        public void Dispose()
        {
            _db.Dispose();
            _scylla.Dispose();
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private BlockRunService Runs() => new(_scylla.Context(), new BlockRunQueue(Options.Create(_options)), new BlockRunRegistry(),
            Options.Create(_options), _time, new BlockProgramValidator(Types), new NoWorkflows());

        private WorkflowService Workflows() => new(new WorkflowRepository(_db), new UserRepository(_db), Runs(),
            new BlockProgramValidator(Types), Options.Create(_options), _time, new NoTriggers());

        private static BlockProgram Program(string text) => JsonSerializer.Deserialize<BlockProgram>($$"""
            { "blocks": [ { "id": 1, "type": "ManualTrigger", "params": {} },
                          { "id": 2, "type": "Print", "params": { "text": "{{text}}" } } ],
              "connections": [ { "from": 1, "to": 2 } ] }
            """, BlockParamsBinder.Options) ?? throw new InvalidOperationException("The test program did not parse.");

        private async Task<List<WorkflowVersion>> Versions(Guid id)
            => (await Workflows().VersionsAsync(_owner.Id, id, Ct)).Ok();

        [Fact]
        public async Task Create_MakesVersionOne_WithTheProgramsHash()
        {
            var created = (await Workflows().CreateAsync(_owner.Id, null, "flow", Program("a"), false, Ct)).Ok();

            var v1 = Assert.Single(await Versions(created.Workflow.Id));
            Assert.Equal(BlockProgramHash.Of(Program("a")), created.Workflow.ContentHash);
            Assert.Equal(created.Workflow.ContentHash, v1.ContentHash);
            Assert.Null(v1.ParentVersion);
        }

        [Fact]
        public async Task Saves_LinkEachVersionToTheOneBefore_AndAnUnchangedProgramMakesNone()
        {
            var id = (await Workflows().CreateAsync(_owner.Id, null, "flow", Program("a"), false, Ct)).Ok().Workflow.Id;

            await Workflows().UpdateAsync(_owner.Id, null, id, "renamed", Program("a"), true, null, Ct);
            await Workflows().UpdateAsync(_owner.Id, null, id, "renamed", Program("b"), true, null, Ct);

            var versions = await Versions(id);
            Assert.Equal([2, 1], versions.Select(v => v.Version));
            Assert.Equal(1, versions[0].ParentVersion);
            Assert.Equal(BlockProgramHash.Of(Program("b")), versions[0].ContentHash);
        }

        [Fact]
        public async Task Restore_IsANewVersion_MarkedWithWhereItCameFrom_AndTheSameHash()
        {
            var id = (await Workflows().CreateAsync(_owner.Id, null, "flow", Program("a"), false, Ct)).Ok().Workflow.Id;
            await Workflows().UpdateAsync(_owner.Id, null, id, "flow", Program("b"), false, null, Ct);

            var restored = (await Workflows().RestoreAsync(_owner.Id, null, id, 1, null, Ct)).Ok();

            var v3 = (await Versions(id))[0];
            Assert.Equal((3, 2, 1), (v3.Version, v3.ParentVersion, v3.RestoredFrom));
            Assert.Equal(BlockProgramHash.Of(Program("a")), v3.ContentHash);
            Assert.Equal(v3.ContentHash, restored.Workflow.ContentHash);
        }

        [Fact]
        public async Task AWorkflowFromBeforeHashes_IsComparedByItsStoredProgram()
        {
            var id = (await Workflows().CreateAsync(_owner.Id, null, "flow", Program("a"), false, Ct)).Ok().Workflow.Id;
            var stored = await _db.Workflows.SingleAsync(w => w.Id == id, Ct);
            stored.ContentHash = null;
            await _db.SaveChangesAsync(Ct);

            await Workflows().UpdateAsync(_owner.Id, null, id, "flow", Program("a"), false, null, Ct);

            Assert.Single(await Versions(id));
            Assert.NotNull((await _db.Workflows.SingleAsync(w => w.Id == id, Ct)).ContentHash);
        }

        [Fact]
        public async Task ARun_NamesTheProgramItRan()
        {
            var program = Program("a");

            var run = (await Runs().StartAsync(_owner.Id, null, new BlockRunStart(program), Ct)).Ok();

            Assert.Equal(BlockProgramHash.Of(program), run.ProgramHash);
        }

        [Fact]
        public async Task ExportThenImport_GivesTheSameProgram_Inactive()
        {
            var id = (await Workflows().CreateAsync(_owner.Id, null, "Nightly sync / v2", Program("a"), true, Ct)).Ok().Workflow.Id;

            var export = (await Workflows().ExportAsync(_owner.Id, id, null, Ct)).Ok();
            var imported = (await Workflows().ImportAsync(_owner.Id, null, export.Json, null, Ct)).Ok();

            Assert.Equal("Nightly-sync---v2-v1.json", export.FileName);
            Assert.Equal(BlockProgramHash.Of(Program("a")), export.ContentHash);
            Assert.True(imported.Intact);
            Assert.Equal("Nightly sync / v2", imported.Details.Workflow.Name);
            Assert.False(imported.Details.Workflow.Active);
            Assert.Equal(export.ContentHash, imported.Details.Workflow.ContentHash);
            Assert.NotEqual(id, imported.Details.Workflow.Id);
        }

        [Fact]
        public async Task AFileEditedAfterExport_ImportsButIsNotIntact()
        {
            var id = (await Workflows().CreateAsync(_owner.Id, null, "flow", Program("a"), false, Ct)).Ok().Workflow.Id;
            var json = (await Workflows().ExportAsync(_owner.Id, id, null, Ct)).Ok().Json.Replace("\"a\"", "\"edited\"");

            var imported = (await Workflows().ImportAsync(_owner.Id, null, json, "copy", Ct)).Ok();

            Assert.False(imported.Intact);
            Assert.Equal("copy", imported.Details.Workflow.Name);
        }

        [Fact]
        public async Task AnOldVersion_Exports()
        {
            var id = (await Workflows().CreateAsync(_owner.Id, null, "flow", Program("a"), false, Ct)).Ok().Workflow.Id;
            await Workflows().UpdateAsync(_owner.Id, null, id, "flow", Program("b"), false, null, Ct);

            var export = (await Workflows().ExportAsync(_owner.Id, id, 1, Ct)).Ok();

            Assert.Equal(BlockProgramHash.Of(Program("a")), export.ContentHash);
            Assert.EndsWith("-v1.json", export.FileName);
            Assert.False((await Workflows().ExportAsync(Guid.NewGuid(), id, null, Ct)).Successful);
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("""{"format":2,"name":"x","program":{}}""")]
        [InlineData("""{"name":"x"}""")]
        public async Task ABadFile_IsRefused(string json)
        {
            var result = await Workflows().ImportAsync(_owner.Id, null, json, null, Ct);

            Assert.Equal(WorkflowErrors.Invalid, result.Error.Code);
        }

        private sealed class NoWorkflows : EchoPhase.Runners.Blocks.IWorkflowSource
        {
            public Task<BlockProgram?> FindAsync(Guid userId, Guid workflowId, CancellationToken ct)
                => Task.FromResult<BlockProgram?>(null);
        }

        private sealed class NoTriggers : IWorkflowTriggerReloader
        {
            public Task ReloadUserAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;
        }
    }
}
