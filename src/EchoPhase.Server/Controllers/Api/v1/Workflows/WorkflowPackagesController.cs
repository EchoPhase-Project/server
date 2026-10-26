// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using EchoPhase.DAL.Postgres.Configurations;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Blocks;
using EchoPhase.Server.Controllers.Api.v1.Common;
using EchoPhase.Server.Services.Blocks;
using EchoPhase.Server.Services.Workflows;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Workflows
{
    /// <param name="Id">The user's id.</param>
    /// <param name="UserName">Their username.</param>
    /// <param name="Name">Their display name.</param>
    public sealed record PackageUserResponse(Guid Id, string? UserName, string Name)
    {
        public static PackageUserResponse From(User? user, Guid id) => new(id, user?.UserName, user?.Name ?? string.Empty);
    }

    /// <param name="Id">The package's id.</param>
    /// <param name="Name">Its name.</param>
    /// <param name="Summary">One or two sentences for its card.</param>
    /// <param name="Readme">Markdown; render it without raw HTML.</param>
    /// <param name="Visibility">Private (owner and shares), Unlisted (anyone with the link) or Public (listed in Explore).</param>
    /// <param name="Tags">The author's tags.</param>
    /// <param name="PackageId">The package it was installed from (it may be gone since).</param>
    /// <param name="Version">The version installed.</param>
    public sealed record PackageSourceResponse(Guid PackageId, int? Version);

    /// <param name="LatestVersion">The newest version's number; 0: never published.</param>
    /// <param name="Installs">How many users (its owner aside) installed it.</param>
    /// <param name="Workflows">How many workflows it has now; 0 for others' packages (they see versions).</param>
    /// <param name="ErrorWorkflow">The key of its workflow started when an automatic run fails; null: none.</param>
    /// <param name="Source">Where it was installed from; null: made here.</param>
    /// <param name="Access">What the caller may do: Owner or Read (read and install).</param>
    /// <param name="Owner">Whose it is.</param>
    /// <param name="Stamp">Changes on every save: send it back as expectedStamp to save only over this version.</param>
    /// <param name="CreatedAt">When it was created.</param>
    /// <param name="UpdatedAt">When it or its versions last changed.</param>
    public sealed record PackageResponse(Guid Id, string Name, string Summary, string Readme, PackageVisibility Visibility,
        string[] Tags, int LatestVersion, int Installs, int Workflows, Guid? ErrorWorkflow, PackageSourceResponse? Source,
        PackageAccess Access, PackageUserResponse Owner, Guid Stamp, DateTime CreatedAt, DateTime UpdatedAt)
    {
        public static PackageResponse From(PackageView view)
        {
            var p = view.Package;
            var owner = view.Access == PackageAccess.Owner;
            return new(p.Id, p.Name, p.Summary, p.Readme, p.Visibility, p.Tags, p.LatestVersion, view.Installs, view.Workflows,
                owner ? p.ErrorWorkflowKey : null,
                owner && p.SourcePackageId is { } source ? new PackageSourceResponse(source, p.SourceVersion) : null,
                view.Access, PackageUserResponse.From(p.Owner, p.OwnerId), p.ConcurrencyStamp, p.CreatedAt, p.UpdatedAt);
        }
    }

    /// <param name="Version">Its number: 1, 2, … in publish order.</param>
    /// <param name="Changelog">What changed, Markdown.</param>
    /// <param name="ContentHash">SHA-256 of its workflows and connections.</param>
    /// <param name="CreatedAt">When it was published.</param>
    public sealed record PackageVersionResponse(int Version, string Changelog, string ContentHash, DateTime CreatedAt)
    {
        public static PackageVersionResponse From(WorkflowPackageVersion v) => new(v.Version, v.Changelog, v.ContentHash, v.CreatedAt);
    }

    /// <param name="Version">Its number.</param>
    /// <param name="Changelog">What changed, Markdown.</param>
    /// <param name="ContentHash">SHA-256 of its workflows and connections.</param>
    /// <param name="CreatedAt">When it was published.</param>
    /// <param name="Workflows">Its workflows; their programs call each other by key, and webhook secrets are empty.</param>
    /// <param name="Connections">The connections they use: an install names one of the installer's for each.</param>
    /// <param name="ErrorWorkflow">The key of the workflow started when an automatic run fails; null: none.</param>
    public sealed record PackageVersionContentResponse(int Version, string Changelog, string ContentHash, DateTime CreatedAt,
        IReadOnlyList<PackageWorkflow> Workflows, IReadOnlyList<PackageConnection> Connections, Guid? ErrorWorkflow)
    {
        public static PackageVersionContentResponse From(PackageVersionDetails d) => new(d.Version.Version, d.Version.Changelog,
            d.Version.ContentHash, d.Version.CreatedAt, d.Content.Workflows, d.Content.Connections, d.Content.ErrorWorkflow);
    }

    /// <param name="Name">Its name.</param>
    /// <param name="Summary">One or two sentences for its card.</param>
    /// <param name="Readme">Markdown.</param>
    /// <param name="Visibility">Private, Unlisted or Public.</param>
    /// <param name="Tags">Up to 10: letters, digits and dashes (spaces become dashes, case is dropped).</param>
    /// <param name="ErrorWorkflow">On update: the key of one of its workflows with an ErrorTrigger, started when an automatic run fails; absent: none.</param>
    /// <param name="ExpectedStamp">On update: the stamp it was read with; 409 when someone saved since.</param>
    public sealed record PackageRequest(
        [Required, MaxLength(WorkflowPackageConfiguration.MaxNameLength)] string Name,
        [MaxLength(WorkflowPackageConfiguration.MaxSummaryLength)] string? Summary = null,
        [MaxLength(WorkflowPackageConfiguration.MaxReadmeLength)] string? Readme = null,
        PackageVisibility Visibility = PackageVisibility.Private,
        string[]? Tags = null,
        Guid? ErrorWorkflow = null,
        Guid? ExpectedStamp = null)
    {
        public PackageInput ToInput() => new(Name, Summary, Readme, Visibility, Tags, ErrorWorkflow, ExpectedStamp);
    }

    /// <param name="Workflows">
    /// Which of its workflows the version holds (ids), the main one first
    /// unless `main` says; absent or empty: all. What a chosen one calls, and
    /// the error workflow, must be chosen too.
    /// </param>
    /// <param name="Main">The one to open first.</param>
    /// <param name="Changelog">What changed, Markdown.</param>
    public sealed record PublishPackageRequest(
        [MaxLength(WorkflowPackageService.MaxWorkflows)] Guid[]? Workflows = null,
        Guid? Main = null,
        [MaxLength(WorkflowPackageVersionConfiguration.MaxChangelogLength)] string? Changelog = null)
    {
        public PackagePublish ToPublish() => new(Workflows, Main, Changelog);
    }

    /// <param name="Version">Which version; absent: the latest.</param>
    /// <param name="Connections">For each connection the package uses (by its name), one of the caller's; absent: one by the same name, if any.</param>
    public sealed record InstallPackageRequest(int? Version = null, Dictionary<string, string>? Connections = null);

    /// <param name="Package">The caller's new package.</param>
    /// <param name="Workflows">Its workflows, inactive.</param>
    /// <param name="Version">The version installed.</param>
    /// <param name="MissingConnections">Connections the caller has no match for: create them (or edit the workflows) before turning them on.</param>
    public sealed record PackageInstallResponse(PackageResponse Package, IReadOnlyList<WorkflowSummaryResponse> Workflows, int Version,
        IReadOnlyList<PackageConnection> MissingConnections);

    /// <param name="Tag">The tag, as kept: lower case, dashes.</param>
    /// <param name="Packages">How many public packages carry it.</param>
    public sealed record PackageTagResponse(string Tag, int Packages);

    /// <param name="UserName">Whom to share it with.</param>
    public sealed record SharePackageRequest([Required] string UserName);

    /// <param name="User">Whom it is shared with.</param>
    /// <param name="CreatedAt">Since when.</param>
    public sealed record PackageShareResponse(PackageUserResponse User, DateTime CreatedAt)
    {
        public static PackageShareResponse From(WorkflowPackageShare share) => new(PackageUserResponse.From(share.User, share.UserId), share.CreatedAt);
    }

    /// <summary>
    /// Packages: the caller's workflows that belong together (made in them
    /// with api/v1/workflows), published as versions others read and install
    /// as packages of their own (an edit is always of their own). A package
    /// the caller may not see answers 404; one they may see but not change, 403.
    /// </summary>
    [Route("api/v1/workflow-packages")]
    public class WorkflowPackagesController : ApiControllerBase
    {
        private readonly WorkflowPackageService _packages;
        private readonly BlockRunService _runs;

        public WorkflowPackagesController(WorkflowPackageService packages, BlockRunService runs)
        {
            _packages = packages;
            _runs = runs;
        }

        /// <summary>The caller's packages, then those shared with them.</summary>
        [HttpGet]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<PackageResponse[]>(StatusCodes.Status200OK)]
        public async Task<IActionResult> List(CancellationToken ct)
            => Ok((await _packages.ListAsync(User.GetUserId(), ct)).Select(PackageResponse.From));

        /// <summary>
        /// Public packages, most installed first, a page at a time (pass the
        /// answer's nextCursor as `cursor`); `q` searches name and summary, `tag` filters.
        /// </summary>
        [HttpGet("explore")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<CursorPageResponse<PackageResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Explore([MaxLength(100)] string? q, [MaxLength(WorkflowPackageConfiguration.MaxTagLength)] string? tag,
            [MaxLength(200)] string? cursor, [Range(1, 100)] int limit = 30, CancellationToken ct = default)
        {
            var result = await _packages.ExploreAsync(User.GetUserId(), q, tag, cursor, limit, ct);
            return result.Successful
                ? Ok(new CursorPageResponse<PackageResponse>(result.Value.Items.Select(PackageResponse.From).ToList(), result.Value.NextCursor))
                : Failure(result.Error);
        }

        /// <summary>
        /// The tags of public packages, most used first, for a search's
        /// suggestions (`tag:`); `q` keeps those starting with it.
        /// </summary>
        [HttpGet("explore/tags")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<PackageTagResponse[]>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Tags([MaxLength(WorkflowPackageConfiguration.MaxTagLength)] string? q,
            [Range(1, 50)] int limit = 10, CancellationToken ct = default)
            => Ok((await _packages.TagsAsync(q, limit, ct)).Select(t => new PackageTagResponse(t.Tag, t.Packages)));

        [HttpGet("{id:guid}")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<PackageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var result = await _packages.GetAsync(User.GetUserId(), id, ct);
            return result.Successful ? Ok(PackageResponse.From(result.Value)) : Failure(result.Error);
        }

        /// <summary>A new, empty package: make workflows in it, then publish versions of it.</summary>
        [HttpPost]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Create)]
        [ProducesResponseType<PackageResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] PackageRequest dto, CancellationToken ct)
        {
            var result = await _packages.CreateAsync(User.GetUserId(), dto.ToInput(), ct);
            return result.Successful
                ? CreatedAtAction(nameof(Get), new { id = result.Value.Package.Id }, PackageResponse.From(result.Value))
                : Failure(result.Error);
        }

        /// <summary>Changes name, summary, README, visibility, tags and error workflow; versions stay as published.</summary>
        [HttpPut("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Update)]
        [ProducesResponseType<PackageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update(Guid id, [FromBody] PackageRequest dto, CancellationToken ct)
        {
            var result = await _packages.UpdateAsync(User.GetUserId(), id, dto.ToInput(), ct);
            return result.Successful ? Ok(PackageResponse.From(result.Value)) : Failure(result.Error);
        }

        /// <summary>Deletes the package with its workflows; packages others installed from it stay theirs.</summary>
        [HttpDelete("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var result = await _packages.DeleteAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }

        /// <summary>Its versions, newest first.</summary>
        [HttpGet("{id:guid}/versions")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<PackageVersionResponse[]>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Versions(Guid id, CancellationToken ct)
        {
            var result = await _packages.VersionsAsync(User.GetUserId(), id, ct);
            return result.Successful ? Ok(result.Value.Select(PackageVersionResponse.From)) : Failure(result.Error);
        }

        /// <summary>A version with its workflows; the latest without `version`.</summary>
        [HttpGet("{id:guid}/content")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<PackageVersionContentResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Content(Guid id, int? version, CancellationToken ct)
        {
            var result = await _packages.VersionAsync(User.GetUserId(), id, version, ct);
            return result.Successful ? Ok(PackageVersionContentResponse.From(result.Value)) : Failure(result.Error);
        }

        /// <summary>A new version from its workflows as they are now (all, or those chosen); 409 when nothing changed.</summary>
        [HttpPost("{id:guid}/versions")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Update)]
        [ProducesResponseType<PackageVersionResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Publish(Guid id, [FromBody] PublishPackageRequest dto, CancellationToken ct)
        {
            var result = await _packages.PublishAsync(User.GetUserId(), User.GetActorId(), id, dto.ToPublish(), ct);
            return result.Successful
                ? CreatedAtAction(nameof(Content), new { id, version = result.Value.Version.Version },
                    PackageVersionResponse.From(result.Value.Version))
                : Failure(result.Error);
        }

        /// <summary>
        /// A new package of the caller's from a version: its workflows,
        /// inactive, pointed at the caller's connections; webhooks get new secrets.
        /// </summary>
        [HttpPost("{id:guid}/install")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Create)]
        [ProducesResponseType<PackageInstallResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Install(Guid id, [FromBody] InstallPackageRequest? dto, CancellationToken ct)
        {
            var result = await _packages.InstallAsync(User.GetUserId(), User.GetActorId(), id, dto?.Version, dto?.Connections, ct);
            if (!result.Successful)
                return Failure(result.Error);

            var install = result.Value;
            return CreatedAtAction(nameof(Get), new { id = install.Package.Package.Id }, new PackageInstallResponse(
                PackageResponse.From(install.Package),
                install.Workflows.Select(w => WorkflowSummaryResponse.From(w.Workflow)).ToList(),
                install.Version.Version.Version,
                install.MissingConnections));
        }

        /// <summary>The runs of its workflows, newest first, a page at a time (pass nextCursor as cursor); its owner's only.</summary>
        [HttpGet("{id:guid}/runs")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<BlockRunPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Runs(Guid id, [Range(1, 100)] int limit = 20, [MaxLength(512)] string? cursor = null,
            CancellationToken ct = default)
        {
            var package = await _packages.GetAsync(User.GetUserId(), id, ct);
            if (!package.Successful)
                return Failure(package.Error);
            if (package.Value.Access != PackageAccess.Owner)
                return Error(ErrorCodes.Forbidden, "Only the package's owner sees its runs.", StatusCodes.Status403Forbidden);

            var page = await _runs.ListByPackageAsync(User.GetUserId(), id, limit, cursor, ct);
            return page.Successful
                ? Ok(new BlockRunPageResponse(page.Value.Items.Select(BlockRunResponse.From).ToList(), page.Value.NextCursor))
                : Failure(page.Error);
        }

        /// <summary>Whom a private package is shared with: only its owner may see.</summary>
        [HttpGet("{id:guid}/shares")]
        [RequirePermission(Resources.Workflows, Permissions.Read)]
        [ProducesResponseType<PackageShareResponse[]>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Shares(Guid id, CancellationToken ct)
        {
            var result = await _packages.SharesAsync(User.GetUserId(), id, ct);
            return result.Successful ? Ok(result.Value.Select(PackageShareResponse.From)) : Failure(result.Error);
        }

        /// <summary>Lets a user read and install the package.</summary>
        [HttpPut("{id:guid}/shares")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Update)]
        [ProducesResponseType<PackageShareResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Share(Guid id, [FromBody] SharePackageRequest dto, CancellationToken ct)
        {
            var result = await _packages.ShareAsync(User.GetUserId(), id, dto.UserName, ct);
            return result.Successful ? Ok(PackageShareResponse.From(result.Value)) : Failure(result.Error);
        }

        /// <summary>Stops sharing with a user: its owner may, and so may that user (to leave it).</summary>
        [HttpDelete("{id:guid}/shares/{userId:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Workflows, Permissions.Update)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Unshare(Guid id, Guid userId, CancellationToken ct)
        {
            var result = await _packages.UnshareAsync(User.GetUserId(), id, userId, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }
    }
}
