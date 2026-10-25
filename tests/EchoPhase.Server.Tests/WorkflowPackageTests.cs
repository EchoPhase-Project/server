// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System.Text.Json;
using EchoPhase.Configuration.Runner;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
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
    /// <summary>The links a program has to its owner's things, read and moved.</summary>
    public class WorkflowLinksTests
    {
        private static readonly Guid Other = Guid.NewGuid();
        private static readonly Guid ErrorFlow = Guid.NewGuid();

        private static BlockProgram Program() => WorkflowPackageTests.Parse($$"""
            { "blocks": [
                { "id": 1, "type": "WebhookTrigger", "params": { "secret": "the-authors-own-secret" } },
                { "id": 2, "type": "ExecuteWorkflow", "params": { "workflowId": "{{Other}}" } },
                { "id": 3, "type": "DiscordGetUserGuilds", "params": { "connection": "bot" } } ],
              "settings": { "errorWorkflowId": "{{ErrorFlow}}" } }
            """);

        [Fact]
        public void Of_FindsOtherWorkflowsAndConnections()
        {
            var links = WorkflowLinks.Of(Program());

            Assert.Equal(new HashSet<Guid> { Other, ErrorFlow }, links.Workflows);
            Assert.Equal(["bot"], links.Connections);
        }

        [Fact]
        public void Rewrite_MovesWorkflowsConnectionsAndSecrets()
        {
            Guid other2 = Guid.NewGuid(), error2 = Guid.NewGuid();
            var moved = WorkflowLinks.Rewrite(Program(), new LinkRewrite(
                new Dictionary<Guid, Guid> { [Other] = other2, [ErrorFlow] = error2 },
                new Dictionary<string, string> { ["bot"] = "my-bot" },
                () => "a-new-secret-for-the-copy"));

            Assert.Equal(error2, moved.Settings.ErrorWorkflowId);
            var links = WorkflowLinks.Of(moved);
            Assert.Equal(new HashSet<Guid> { other2, error2 }, links.Workflows);
            Assert.Equal(["my-bot"], links.Connections);
            var webhook = (WebhookTriggerParams)BlockParamsBinder.Bind(typeof(WebhookTriggerParams),
                moved.Blocks.Single(b => b.Type == BlockTypes.WebhookTrigger).Params);
            Assert.Equal("a-new-secret-for-the-copy", webhook.Secret);
        }

        [Fact]
        public void Rewrite_LeavesWhatItIsNotTold()
        {
            var program = Program();
            var same = WorkflowLinks.Rewrite(program, new LinkRewrite(new Dictionary<Guid, Guid>(), new Dictionary<string, string>()));

            Assert.Equal(BlockProgramHash.Of(program), BlockProgramHash.Of(same));
        }
    }

    /// <summary>Packages: published from workflows that belong together, read by others, installed as copies.</summary>
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

        private WorkflowPackageService Packages() => new(_db, Workflows(), _time);

        // Ann's pair: "main" runs "on error" and names it its error workflow.
        private async Task<(Guid Main, Guid OnError)> PairAsync(string text = "failed")
        {
            var onError = (await Workflows().CreateAsync(_ann.Id, null, "on error", Parse($$"""
                { "blocks": [ { "id": 1, "type": "ErrorTrigger", "params": {} },
                              { "id": 2, "type": "Print", "params": { "text": "{{text}}" } } ],
                  "connections": [ { "from": 1, "to": 2 } ] }
                """), false, Ct)).Ok().Workflow.Id;
            var main = (await Workflows().CreateAsync(_ann.Id, null, "main", Parse($$"""
                { "blocks": [ { "id": 1, "type": "WebhookTrigger", "params": { "secret": "the-authors-own-secret" } },
                              { "id": 2, "type": "ExecuteWorkflow", "params": { "workflowId": "{{onError}}" } },
                              { "id": 3, "type": "DiscordGetUserGuilds", "params": { "connection": "bot" } } ],
                  "connections": [ { "from": 1, "to": 2 }, { "from": 2, "to": 3 } ],
                  "settings": { "errorWorkflowId": "{{onError}}" } }
                """), false, Ct)).Ok().Workflow.Id;
            return (main, onError);
        }

        private static PackageInput Input(PackageVisibility visibility = PackageVisibility.Private, params string[] tags)
            => new("Guild tools", "Lists guilds.", "# Guild tools", visibility, tags);

        private async Task<Guid> PublishPairAsync(PackageVisibility visibility = PackageVisibility.Private, params string[] tags)
        {
            var (main, onError) = await PairAsync();
            return (await Packages().CreateAsync(_ann.Id, null, Input(visibility, tags), new PackagePublish([main, onError], null, "First."), Ct))
                .Ok().Package.Id;
        }

        [Fact]
        public async Task Publish_RefusesAWorkflowThatUsesOneLeftOut()
        {
            var (main, _) = await PairAsync();

            var result = await Packages().CreateAsync(_ann.Id, null, Input(), new PackagePublish([main], null, null), Ct);

            Assert.Equal(ErrorCodes.Invalid, result.Error.Code);
            Assert.Contains("not in the package", result.Error.Message);
        }

        [Fact]
        public async Task Version_HoldsTheWorkflowsWithoutSecrets_AndTheConnectionsTheyNeed()
        {
            var id = await PublishPairAsync();

            var version = (await Packages().VersionAsync(_ann.Id, id, null, Ct)).Ok();
            Assert.Equal(1, version.Version.Version);
            Assert.Equal(version.Content.Hash, version.Version.ContentHash);
            Assert.Equal(["main", "on error"], version.Content.Workflows.Select(w => w.Name));
            Assert.True(version.Content.Workflows[0].Main);
            Assert.DoesNotContain("the-authors-own-secret", version.Version.Content);
            Assert.Equal([new PackageConnection("bot", "discord")], version.Content.Connections);
        }

        [Fact]
        public async Task Private_IsSeenOnlyByThoseItIsSharedWith_AndOnlyTheOwnerChangesIt()
        {
            var id = await PublishPairAsync();

            Assert.Equal(ErrorCodes.NotFound, (await Packages().GetAsync(_bob.Id, id, Ct)).Error.Code);
            (await Packages().ShareAsync(_ann.Id, id, "bob", Ct)).Ok();

            var seen = (await Packages().GetAsync(_bob.Id, id, Ct)).Ok();
            Assert.Equal(PackageAccess.Read, seen.Access);
            Assert.Contains(await Packages().ListAsync(_bob.Id, Ct), v => v.Package.Id == id);
            Assert.Equal(ErrorCodes.Forbidden, (await Packages().UpdateAsync(_bob.Id, id, Input(), Ct)).Error.Code);
            Assert.Equal(ErrorCodes.Forbidden, (await Packages().DeleteAsync(_bob.Id, id, Ct)).Error.Code);

            // Bob may leave it.
            Assert.True((await Packages().UnshareAsync(_bob.Id, id, _bob.Id, Ct)).Successful);
            Assert.Equal(ErrorCodes.NotFound, (await Packages().GetAsync(_bob.Id, id, Ct)).Error.Code);
        }

        [Fact]
        public async Task Unlisted_IsSeenByAnyone_ButNotListed()
        {
            var id = await PublishPairAsync(PackageVisibility.Unlisted);

            (await Packages().GetAsync(_bob.Id, id, Ct)).Ok();
            Assert.Empty(await Packages().ExploreAsync(_bob.Id, null, null, 50, Ct));
        }

        [Fact]
        public async Task Install_CopiesTheWorkflows_LinkedToEachOther_WithTheInstallersConnection()
        {
            var id = await PublishPairAsync(PackageVisibility.Public);
            _db.Connections.Add(new Connection { UserId = _bob.Id, Provider = "discord", Name = "my-bot" });
            await _db.SaveChangesAsync(Ct);

            var install = (await Packages().InstallAsync(_bob.Id, null, id, null,
                new Dictionary<string, string> { ["bot"] = "my-bot" }, Ct)).Ok();

            Assert.Empty(install.MissingConnections);
            var copies = install.Workflows.ToDictionary(w => w.Workflow.Name);
            var main = copies["main"];
            var onError = copies["on error"];
            Assert.All(install.Workflows, w =>
            {
                Assert.Equal(_bob.Id, w.Workflow.UserId);
                Assert.False(w.Workflow.Active);
                Assert.Equal((id, 1), (w.Workflow.SourcePackageId, w.Workflow.SourcePackageVersion));
            });

            var links = WorkflowLinks.Of(main.Program);
            Assert.Equal(new HashSet<Guid> { onError.Workflow.Id }, links.Workflows);
            Assert.Equal(onError.Workflow.Id, main.Program.Settings.ErrorWorkflowId);
            Assert.Equal(["my-bot"], links.Connections);
            var secret = ((WebhookTriggerParams)BlockParamsBinder.Bind(typeof(WebhookTriggerParams),
                main.Program.Blocks.Single(b => b.Type == BlockTypes.WebhookTrigger).Params)).Secret;
            Assert.NotEqual("the-authors-own-secret", secret);
            Assert.True(secret.Length >= WebhookTriggerParams.MinSecretLength);

            // Saved: Bob's to edit, with a first version.
            Assert.Single((await Workflows().VersionsAsync(_bob.Id, main.Workflow.Id, Ct)).Ok());
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
        public async Task Publish_AgainWithoutChanges_Conflicts_AndAChangeIsTheNextVersion()
        {
            var (main, onError) = await PairAsync();
            var publish = new PackagePublish([main, onError], null, null);
            var id = (await Packages().CreateAsync(_ann.Id, null, Input(), publish, Ct)).Ok().Package.Id;

            Assert.Equal(ErrorCodes.Conflict, (await Packages().PublishAsync(_ann.Id, null, id, publish, Ct)).Error.Code);

            var workflow = (await Workflows().GetAsync(_ann.Id, onError, Ct)).Ok();
            workflow.Program.Blocks[1].Params = JsonSerializer.SerializeToElement(new { text = "changed" });
            (await Workflows().UpdateAsync(_ann.Id, null, onError, "on error", workflow.Program, false, null, Ct)).Ok();

            var v2 = (await Packages().PublishAsync(_ann.Id, null, id, publish with { Changelog = "Louder." }, Ct)).Ok();
            Assert.Equal(2, v2.Version.Version);
            Assert.Equal([2, 1], (await Packages().VersionsAsync(_ann.Id, id, Ct)).Ok().Select(v => v.Version));
        }

        [Fact]
        public async Task Explore_ListsPublicPackages_ByTag()
        {
            var id = await PublishPairAsync(PackageVisibility.Public, "Discord Bot", "guilds");
            await PublishPairAsync(PackageVisibility.Private, "discord-bot");

            var all = await Packages().ExploreAsync(_bob.Id, null, null, 50, Ct);
            Assert.Equal([id], all.Select(v => v.Package.Id));
            Assert.Equal(["discord-bot", "guilds"], all[0].Package.Tags);
            Assert.Single(await Packages().ExploreAsync(_bob.Id, null, "discord-bot", 50, Ct));
            Assert.Empty(await Packages().ExploreAsync(_bob.Id, null, "twitch", 50, Ct));
        }

        [Fact]
        public async Task Tags_MustBeWords()
        {
            var (main, onError) = await PairAsync();

            var result = await Packages().CreateAsync(_ann.Id, null, Input(PackageVisibility.Public, "c#"),
                new PackagePublish([main, onError], null, null), Ct);

            Assert.Equal("tags", result.Error.Field);
        }
    }
}
