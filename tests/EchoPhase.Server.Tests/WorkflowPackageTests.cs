// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System.Text.Json;
using EchoPhase.Configuration.Runner;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Runners.Blocks.Runs;
using EchoPhase.Runners.Blocks.Validation;
using EchoPhase.Server.Services.Blocks;
using EchoPhase.Server.Services.Workflows;
using EchoPhase.Types.Result;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace EchoPhase.Server.Tests
{
    /// <summary>The links a program has to its package and its owner's things, read and moved.</summary>
    public class WorkflowLinksTests
    {
        private static readonly Guid Other = Guid.NewGuid();

        private static BlockProgram Program() => WorkflowPackageTests.Parse($$"""
            { "blocks": [
                { "id": 1, "type": "WebhookTrigger", "params": { "secret": "the-authors-own-secret" } },
                { "id": 2, "type": "ExecuteWorkflow", "params": { "workflow": "{{Other}}" } },
                { "id": 3, "type": "DiscordGetUserGuilds", "params": { "connection": "bot" } } ] }
            """);

        [Fact]
        public void Of_FindsCalledWorkflowsAndConnections()
        {
            var links = WorkflowLinks.Of(Program());

            Assert.Equal(new HashSet<Guid> { Other }, links.Workflows);
            Assert.Equal(["bot"], links.Connections);
        }

        [Fact]
        public void Rewrite_MovesConnectionsAndSecrets_NotTheWorkflowsCalled()
        {
            var moved = WorkflowLinks.Rewrite(Program(), new LinkRewrite(
                new Dictionary<string, string> { ["bot"] = "my-bot" },
                () => "a-new-secret-for-the-copy"));

            var links = WorkflowLinks.Of(moved);
            Assert.Equal(new HashSet<Guid> { Other }, links.Workflows);
            Assert.Equal(["my-bot"], links.Connections);
            var webhook = (WebhookTriggerParams)BlockParamsBinder.Bind(typeof(WebhookTriggerParams),
                moved.Blocks.Single(b => b.Type == BlockTypes.WebhookTrigger).Params);
            Assert.Equal("a-new-secret-for-the-copy", webhook.Secret);
        }

        [Fact]
        public void Rewrite_LeavesWhatItIsNotTold()
        {
            var program = Program();
            var same = WorkflowLinks.Rewrite(program, new LinkRewrite(new Dictionary<string, string>()));

            Assert.Equal(BlockProgramHash.Of(program), BlockProgramHash.Of(same));
        }
    }

    /// <summary>Packages: workflows that belong together, published, read by others, installed as packages of their own.</summary>
    public class WorkflowPackageTests : IDisposable
    {
        private static readonly BlockTypeRegistry Types = new(typeof(BlockTypeAttribute).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<BlockTypeAttribute>() is not null));

        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
        private readonly PostgresContext _db;
        private readonly ScyllaTestDb _scylla;
        private readonly RunnerOptions _options = new();
        private readonly User _ann = new("Ann") { UserName = "ann", NormalizedUserName = "ANN" };
        private readonly User _bob = new("Bob") { UserName = "bob", NormalizedUserName = "BOB" };

        public WorkflowPackageTests()
        {
            _db = new PostgresContext(new DbContextOptionsBuilder<PostgresContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            _db.Users.AddRange(_ann, _bob);
            _db.Connections.Add(new Connection { UserId = _ann.Id, Provider = "discord", Name = "bot" });
            _db.SaveChanges();
            _scylla = new ScyllaTestDb(_time);
        }

        public void Dispose()
        {
            _db.Dispose();
            _scylla.Dispose();
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        internal static BlockProgram Parse(string json)
            => JsonSerializer.Deserialize<BlockProgram>(json, BlockParamsBinder.Options)
               ?? throw new InvalidOperationException("The test program did not parse.");

        private WorkflowService Workflows() => new(new WorkflowRepository(_db), new UserRepository(_db),
            new BlockRunService(_scylla.Context(), new BlockRunQueue(Options.Create(_options)), new BlockRunRegistry(),
                Options.Create(_options), _time, new BlockProgramValidator(Types), new WorkflowVersionTests.NoWorkflows()),
            new BlockProgramValidator(Types), Options.Create(_options), _time, new WorkflowVersionTests.NoTriggers());

        private WorkflowPackageService Packages() => new(_db, Workflows(), new WorkflowVersionTests.NoTriggers(), _time);

        private static PackageInput Input(PackageVisibility visibility = PackageVisibility.Private, Guid? errorWorkflow = null,
            params string[] tags)
            => new("Guild tools", "Lists guilds.", "# Guild tools", visibility, tags, errorWorkflow);

        private static readonly BlockProgram OnError = Parse("""
            { "blocks": [ { "id": 1, "type": "ErrorTrigger", "params": {} },
                          { "id": 2, "type": "Print", "params": { "text": "failed" } } ],
              "connections": [ { "from": 1, "to": 2 } ] }
            """);

        private static BlockProgram Calling(Guid key) => Parse($$"""
            { "blocks": [ { "id": 1, "type": "WebhookTrigger", "params": { "secret": "the-authors-own-secret" } },
                          { "id": 2, "type": "ExecuteWorkflow", "params": { "workflow": "{{key}}" } },
                          { "id": 3, "type": "DiscordGetUserGuilds", "params": { "connection": "bot" } } ],
              "connections": [ { "from": 1, "to": 2 }, { "from": 2, "to": 3 } ] }
            """);

        private static readonly BlockProgram Solo = Parse("""
            { "blocks": [ { "id": 1, "type": "Print", "params": { "text": "alone" } } ] }
            """);

        private sealed record Pair(Guid Package, Workflow Main, Workflow OnError);

        // Ann's package: "main" calls "on error", which is the package's error workflow.
        private async Task<Pair> PairAsync(PackageVisibility visibility = PackageVisibility.Private, params string[] tags)
        {
            var package = (await Packages().CreateAsync(_ann.Id, Input(visibility, null, tags), Ct)).Ok().Package.Id;
            var onError = (await Workflows().CreateAsync(_ann.Id, null, package, "on error", OnError, false, Ct)).Ok().Workflow;
            var main = (await Workflows().CreateAsync(_ann.Id, null, package, "main", Calling(onError.Key), false, Ct)).Ok().Workflow;
            (await Packages().UpdateAsync(_ann.Id, package, Input(visibility, onError.Key, tags), Ct)).Ok();
            return new Pair(package, main, onError);
        }

        private async Task<Guid> PublishPairAsync(PackageVisibility visibility = PackageVisibility.Private, params string[] tags)
        {
            var pair = await PairAsync(visibility, tags);
            (await Packages().PublishAsync(_ann.Id, null, pair.Package,
                new PackagePublish([pair.Main.Id, pair.OnError.Id], null, "First."), Ct)).Ok();
            Assert.Equal("ann", Must.Present((await Packages().GetAsync(_ann.Id, pair.Package, Ct)).Ok().Package.Owner).UserName);
            return pair.Package;
        }

        [Fact]
        public async Task AWorkflowWithoutAPackage_GetsOneOfItsOwn_OfTheSameName()
        {
            var created = (await Workflows().CreateAsync(_ann.Id, null, null, "alone", Solo, false, Ct)).Ok().Workflow;

            var package = (await Packages().GetAsync(_ann.Id, created.PackageId, Ct)).Ok();
            Assert.Equal("alone", package.Package.Name);
            Assert.Equal(PackageVisibility.Private, package.Package.Visibility);
            Assert.Equal(1, package.Workflows);
        }

        [Fact]
        public async Task AWorkflow_CallsOnlyWorkflowsOfItsPackage()
        {
            var elsewhere = (await Workflows().CreateAsync(_ann.Id, null, null, "elsewhere", Solo, false, Ct)).Ok().Workflow;

            var result = await Workflows().CreateAsync(_ann.Id, null, null, "caller", Calling(elsewhere.Key), false, Ct);

            Assert.Equal(WorkflowErrors.Invalid, result.Error.Code);
            Assert.Contains("not in its package", result.Error.Message);
        }

        [Fact]
        public async Task AnotherUsersPackage_TakesNoWorkflows()
        {
            var package = (await Packages().CreateAsync(_ann.Id, Input(), Ct)).Ok().Package.Id;

            var result = await Workflows().CreateAsync(_bob.Id, null, package, "intruder", Solo, false, Ct);

            Assert.Equal(WorkflowErrors.NotFound, result.Error.Code);
        }

        [Fact]
        public async Task Delete_RefusesAWorkflowThatIsCalled_OrIsTheErrorWorkflow()
        {
            var pair = await PairAsync();

            Assert.Equal(WorkflowErrors.Conflict, (await Workflows().DeleteAsync(_ann.Id, pair.OnError.Id, Ct)).Error.Code);
            Assert.True((await Workflows().DeleteAsync(_ann.Id, pair.Main.Id, Ct)).Successful);
            // Still the error workflow.
            Assert.Equal(WorkflowErrors.Conflict, (await Workflows().DeleteAsync(_ann.Id, pair.OnError.Id, Ct)).Error.Code);

            (await Packages().UpdateAsync(_ann.Id, pair.Package, Input(), Ct)).Ok();
            Assert.True((await Workflows().DeleteAsync(_ann.Id, pair.OnError.Id, Ct)).Successful);
        }

        [Fact]
        public async Task TheErrorWorkflow_NeedsAnErrorTrigger_AndKeepsIt()
        {
            var pair = await PairAsync();

            var wrong = await Packages().UpdateAsync(_ann.Id, pair.Package, Input(errorWorkflow: pair.Main.Key), Ct);
            Assert.Equal("errorWorkflow", wrong.Error.Field);

            var dropped = await Workflows().UpdateAsync(_ann.Id, null, pair.OnError.Id, "on error", Solo, false, null, Ct);
            Assert.Equal(WorkflowErrors.Invalid, dropped.Error.Code);
        }

        [Fact]
        public async Task AFailedRun_StartsThePackagesErrorWorkflow()
        {
            var pair = await PairAsync();
            var failed = new BlockRun
            {
                UserId = _ann.Id,
                RunId = Guid.NewGuid(),
                WorkflowId = pair.Main.Id,
                PackageId = pair.Package,
                Status = BlockRunStatus.Failed,
                Trigger = BlockRunTriggers.Webhook,
            };

            var started = Must.Present((await Workflows().StartErrorWorkflowAsync(failed, [], Ct)).Ok());

            Assert.Equal(pair.OnError.Id, started.WorkflowId);
            Assert.Equal(BlockRunTriggers.Error, started.Trigger);

            // Without an error workflow, nothing starts.
            var alone = (await Workflows().CreateAsync(_ann.Id, null, null, "alone", Solo, false, Ct)).Ok().Workflow;
            var none = await Workflows().StartErrorWorkflowAsync(new BlockRun
            {
                UserId = _ann.Id,
                RunId = Guid.NewGuid(),
                WorkflowId = alone.Id,
                PackageId = alone.PackageId,
                Status = BlockRunStatus.Failed,
                Trigger = BlockRunTriggers.Webhook,
            }, [], Ct);
            Assert.Null(none.Ok());
        }

        [Fact]
        public async Task Publish_RefusesAWorkflowThatCallsOneLeftOut_OrLeavingOutTheErrorWorkflow()
        {
            var pair = await PairAsync();
            var solo = (await Workflows().CreateAsync(_ann.Id, null, pair.Package, "solo", Solo, false, Ct)).Ok().Workflow;

            var calls = await Packages().PublishAsync(_ann.Id, null, pair.Package, new PackagePublish([pair.Main.Id], null, null), Ct);
            Assert.Equal(ErrorCodes.Invalid, calls.Error.Code);
            Assert.Contains("calls workflow", calls.Error.Message);

            var onError = await Packages().PublishAsync(_ann.Id, null, pair.Package, new PackagePublish([solo.Id], null, null), Ct);
            Assert.Contains("error workflow is not chosen", onError.Error.Message);

            // Without a list: all of them.
            var all = (await Packages().PublishAsync(_ann.Id, null, pair.Package, new PackagePublish(null, null, null), Ct)).Ok();
            Assert.Equal(3, all.Content.Workflows.Count);
        }

        [Fact]
        public async Task Version_HoldsTheWorkflowsWithoutSecrets_TheConnectionsTheyNeed_AndTheErrorWorkflow()
        {
            var id = await PublishPairAsync();

            var version = (await Packages().VersionAsync(_ann.Id, id, null, Ct)).Ok();
            Assert.Equal(1, version.Version.Version);
            Assert.Equal(version.Content.Hash, version.Version.ContentHash);
            Assert.Equal(["main", "on error"], version.Content.Workflows.Select(w => w.Name));
            Assert.True(version.Content.Workflows[0].Main);
            Assert.DoesNotContain("the-authors-own-secret", version.Version.Content);
            Assert.Equal([new PackageConnection("bot", "discord")], version.Content.Connections);
            Assert.Equal(version.Content.Workflows[1].Key, version.Content.ErrorWorkflow);
        }

        [Fact]
        public async Task Private_IsSeenOnlyByThoseItIsSharedWith_AndOnlyTheOwnerChangesIt()
        {
            var id = await PublishPairAsync();

            Assert.Equal(ErrorCodes.NotFound, (await Packages().GetAsync(_bob.Id, id, Ct)).Error.Code);
            (await Packages().ShareAsync(_ann.Id, id, "bob", Ct)).Ok();

            var seen = (await Packages().GetAsync(_bob.Id, id, Ct)).Ok();
            Assert.Equal(PackageAccess.Read, seen.Access);
            Assert.Equal(0, seen.Workflows);
            Assert.Contains(await Packages().ListAsync(_bob.Id, Ct), v => v.Package.Id == id);
            Assert.Equal(ErrorCodes.Forbidden, (await Packages().UpdateAsync(_bob.Id, id, Input(), Ct)).Error.Code);
            Assert.Equal(ErrorCodes.Forbidden, (await Packages().DeleteAsync(_bob.Id, id, Ct)).Error.Code);
            Assert.Empty(await Workflows().ListAsync(_bob.Id, id, Ct));

            // Bob may leave it.
            Assert.True((await Packages().UnshareAsync(_bob.Id, id, _bob.Id, Ct)).Successful);
            Assert.Equal(ErrorCodes.NotFound, (await Packages().GetAsync(_bob.Id, id, Ct)).Error.Code);
        }

        [Fact]
        public async Task Unlisted_IsSeenByAnyone_ButNotListed()
        {
            var id = await PublishPairAsync(PackageVisibility.Unlisted);

            (await Packages().GetAsync(_bob.Id, id, Ct)).Ok();
            Assert.Empty((await Packages().ExploreAsync(_bob.Id, null, null, null, 50, Ct)).Ok().Items);
        }

        [Fact]
        public async Task Install_MakesAPackage_WithTheSameKeys_TheErrorWorkflow_AndTheInstallersConnection()
        {
            var id = await PublishPairAsync(PackageVisibility.Public);
            _db.Connections.Add(new Connection { UserId = _bob.Id, Provider = "discord", Name = "my-bot" });
            await _db.SaveChangesAsync(Ct);

            var install = (await Packages().InstallAsync(_bob.Id, null, id, null,
                new Dictionary<string, string> { ["bot"] = "my-bot" }, Ct)).Ok();

            Assert.Empty(install.MissingConnections);
            var package = install.Package.Package;
            Assert.Equal((_bob.Id, "Guild tools", PackageVisibility.Private), (package.OwnerId, package.Name, package.Visibility));
            Assert.Equal((id, 1), (package.SourcePackageId, package.SourceVersion));
            Assert.NotEqual(id, package.Id);

            var copies = install.Workflows.ToDictionary(w => w.Workflow.Name);
            var main = copies["main"];
            var onError = copies["on error"];
            Assert.All(install.Workflows, w =>
            {
                Assert.Equal(_bob.Id, w.Workflow.UserId);
                Assert.Equal(package.Id, w.Workflow.PackageId);
                Assert.False(w.Workflow.Active);
            });
            Assert.Equal(onError.Workflow.Key, package.ErrorWorkflowKey);

            var links = WorkflowLinks.Of(main.Program);
            Assert.Equal(new HashSet<Guid> { onError.Workflow.Key }, links.Workflows);
            Assert.Equal(["my-bot"], links.Connections);
            var secret = ((WebhookTriggerParams)BlockParamsBinder.Bind(typeof(WebhookTriggerParams),
                main.Program.Blocks.Single(b => b.Type == BlockTypes.WebhookTrigger).Params)).Secret;
            Assert.NotEqual("the-authors-own-secret", secret);
            Assert.True(secret.Length >= WebhookTriggerParams.MinSecretLength);

            // Saved: Bob's to edit, with a first version.
            Assert.Single((await Workflows().VersionsAsync(_bob.Id, main.Workflow.Id, Ct)).Ok());
            Assert.Equal(2, (await Packages().GetAsync(_bob.Id, package.Id, Ct)).Ok().Workflows);
        }

        [Fact]
        public async Task Install_TellsWhichConnectionsAreMissing_AndRefusesOnesTheInstallerLacks()
        {
            var id = await PublishPairAsync(PackageVisibility.Public);

            var install = (await Packages().InstallAsync(_bob.Id, null, id, null, null, Ct)).Ok();
            Assert.Equal([new PackageConnection("bot", "discord")], install.MissingConnections);

            var wrong = await Packages().InstallAsync(_bob.Id, null, id, null, new Dictionary<string, string> { ["bot"] = "nope" }, Ct);
            Assert.Equal(ErrorCodes.Invalid, wrong.Error.Code);
        }

        [Fact]
        public async Task Installs_CountEachOtherUserOnce()
        {
            var id = await PublishPairAsync(PackageVisibility.Public);

            await Packages().InstallAsync(_bob.Id, null, id, null, null, Ct);
            await Packages().InstallAsync(_bob.Id, null, id, null, null, Ct);
            await Packages().InstallAsync(_ann.Id, null, id, null, null, Ct);

            Assert.Equal(1, (await Packages().GetAsync(_ann.Id, id, Ct)).Ok().Installs);
        }

        [Fact]
        public async Task Delete_TakesItsWorkflows_ButNotThePackagesInstalledFromIt()
        {
            var id = await PublishPairAsync(PackageVisibility.Public);
            var installed = (await Packages().InstallAsync(_bob.Id, null, id, null, null, Ct)).Ok().Package.Package.Id;

            Assert.True((await Packages().DeleteAsync(_ann.Id, id, Ct)).Successful);

            Assert.Empty(await Workflows().ListAsync(_ann.Id, null, Ct));
            Assert.Equal(2, (await Workflows().ListAsync(_bob.Id, installed, Ct)).Count);
        }

        [Fact]
        public async Task Publish_AgainWithoutChanges_Conflicts_AndAChangeIsTheNextVersion()
        {
            var pair = await PairAsync();
            var publish = new PackagePublish([pair.Main.Id, pair.OnError.Id], null, null);
            (await Packages().PublishAsync(_ann.Id, null, pair.Package, publish, Ct)).Ok();

            Assert.Equal(ErrorCodes.Conflict, (await Packages().PublishAsync(_ann.Id, null, pair.Package, publish, Ct)).Error.Code);

            var workflow = (await Workflows().GetAsync(_ann.Id, pair.OnError.Id, Ct)).Ok();
            workflow.Program.Blocks[1].Params = JsonSerializer.SerializeToElement(new { text = "changed" });
            (await Workflows().UpdateAsync(_ann.Id, null, pair.OnError.Id, "on error", workflow.Program, false, null, Ct)).Ok();

            var v2 = (await Packages().PublishAsync(_ann.Id, null, pair.Package, publish with { Changelog = "Louder." }, Ct)).Ok();
            Assert.Equal(2, v2.Version.Version);
            Assert.Equal([2, 1], (await Packages().VersionsAsync(_ann.Id, pair.Package, Ct)).Ok().Select(v => v.Version));
        }

        [Fact]
        public async Task Explore_ListsPublicPackages_ByTag()
        {
            var id = await PublishPairAsync(PackageVisibility.Public, "Discord Bot", "guilds");
            await PublishPairAsync(PackageVisibility.Private, "discord-bot");
            // Public but never published: nothing to install yet.
            (await Packages().CreateAsync(_ann.Id, Input(PackageVisibility.Public), Ct)).Ok();

            var all = (await Packages().ExploreAsync(_bob.Id, null, null, null, 50, Ct)).Ok().Items;
            Assert.Equal([id], all.Select(v => v.Package.Id));
            Assert.Equal(["discord-bot", "guilds"], all[0].Package.Tags);
            Assert.Single((await Packages().ExploreAsync(_bob.Id, null, "discord-bot", null, 50, Ct)).Ok().Items);
            Assert.Empty((await Packages().ExploreAsync(_bob.Id, null, "twitch", null, 50, Ct)).Ok().Items);
        }

        [Fact]
        public async Task Explore_PagesThroughEveryPackageOnce_MostInstalledFirst()
        {
            var ids = new List<Guid>();
            for (int i = 0; i < 5; i++)
            {
                ids.Add(await PublishPairAsync(PackageVisibility.Public));
                _time.Advance(TimeSpan.FromMinutes(1));
            }
            // The oldest is the only one installed: it leads.
            await Packages().InstallAsync(_bob.Id, null, ids[0], null, null, Ct);

            var seen = new List<Guid>();
            string? cursor = null;
            do
            {
                var page = (await Packages().ExploreAsync(_bob.Id, null, null, cursor, 2, Ct)).Ok();
                Assert.True(page.Items.Count <= 2);
                seen.AddRange(page.Items.Select(v => v.Package.Id));
                cursor = page.NextCursor;
            }
            while (cursor is not null);

            Assert.Equal([ids[0], ids[4], ids[3], ids[2], ids[1]], seen);
            Assert.Equal(ErrorCodes.Invalid, (await Packages().ExploreAsync(_bob.Id, null, null, "not-a-cursor", 2, Ct)).Error.Code);
        }

        [Fact]
        public async Task Tags_OfPublicPackages_MostUsedFirst_ByPrefix()
        {
            await PublishPairAsync(PackageVisibility.Public, "discord-bot", "guilds");
            await PublishPairAsync(PackageVisibility.Public, "discord-bot");
            await PublishPairAsync(PackageVisibility.Private, "secret-tag");

            var all = await Packages().TagsAsync(null, 10, Ct);
            Assert.Equal([new TagCount("discord-bot", 2), new TagCount("guilds", 1)], all);
            Assert.Equal(["discord-bot"], (await Packages().TagsAsync("Disc", 10, Ct)).Select(t => t.Tag));
        }

        [Fact]
        public async Task Tags_MustBeWords()
        {
            var result = await Packages().CreateAsync(_ann.Id, Input(PackageVisibility.Public, null, "c#"), Ct);

            Assert.Equal("tags", result.Error.Field);
        }
    }
}
