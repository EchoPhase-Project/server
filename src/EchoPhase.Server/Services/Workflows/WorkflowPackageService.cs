// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Configurations;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Types.Result;
using Microsoft.EntityFrameworkCore;
using UUIDNext;

namespace EchoPhase.Server.Services.Workflows
{
    /// <summary>What a user may do with a package.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PackageAccess>))]
    public enum PackageAccess
    {
        /// <summary>Read it and install copies.</summary>
        Read,
        /// <summary>Theirs: publish, edit, share, delete.</summary>
        Owner,
    }

    /// <summary>A package as one user sees it.</summary>
    /// <param name="Installs">How many users (its owner aside) installed it.</param>
    public sealed record PackageView(WorkflowPackage Package, PackageAccess Access, int Installs = 0);

    /// <summary>A page of packages and where the next one starts (null: this is the last).</summary>
    public sealed record PackagePage(IReadOnlyList<PackageView> Items, string? NextCursor);

    /// <summary>One version of a package with what it holds.</summary>
    /// <param name="OwnerId">The package's owner.</param>
    public sealed record PackageVersionDetails(WorkflowPackageVersion Version, WorkflowPackageContent Content, Guid OwnerId);

    /// <summary>What a package says about itself; a version is published separately.</summary>
    /// <param name="ExpectedStamp">On update: the stamp it was read with; a newer one fails it.</param>
    public sealed record PackageInput(string Name, string? Summary, string? Readme, PackageVisibility Visibility,
        IReadOnlyList<string>? Tags, Guid? ExpectedStamp = null);

    /// <summary>The workflows of a new version: the publisher's own.</summary>
    /// <param name="Main">The one to open first; null: the first.</param>
    public sealed record PackagePublish(IReadOnlyList<Guid> Workflows, Guid? Main, string? Changelog);

    /// <summary>The copies an install made, and the connections it could not find a match for.</summary>
    public sealed record PackageInstall(PackageVersionDetails Version, IReadOnlyList<WorkflowDetails> Workflows,
        IReadOnlyList<PackageConnection> MissingConnections);

    /// <summary>
    /// Workflows published as packages: fixed versions others read and install
    /// as their own copies, programs pointing at each other and at the
    /// installer's connections. A package someone may not see does not exist
    /// for them (404); one they may see but not manage answers 403.
    /// </summary>
    public class WorkflowPackageService
    {
        /// <summary>Workflows in one version: a set that belongs together, not a library.</summary>
        public const int MaxWorkflows = 20;

        private readonly PostgresContext _db;
        private readonly WorkflowService _workflows;
        private readonly TimeProvider _time;

        public WorkflowPackageService(PostgresContext db, WorkflowService workflows, TimeProvider time)
        {
            _db = db;
            _workflows = workflows;
            _time = time;
        }

        /// <summary>The user's packages, then those shared with them, recently changed first.</summary>
        public async Task<IReadOnlyList<PackageView>> ListAsync(Guid userId, CancellationToken ct = default)
        {
            var packages = _db.WorkflowPackages.AsNoTracking()
                .Where(p => p.OwnerId == userId || p.Shares.Any(s => s.UserId == userId))
                .OrderByDescending(p => p.OwnerId == userId)
                .ThenByDescending(p => p.UpdatedAt);
            return await ViewsAsync(userId, packages, ct);
        }

        /// <summary>
        /// Public packages with at least one version, most installed first,
        /// a page at a time: <paramref name="cursor"/> is the previous page's
        /// next cursor. <paramref name="query"/> matches the name or summary,
        /// ignoring case; <paramref name="tag"/> keeps those with that tag.
        /// </summary>
        public async Task<IServiceResult<PackagePage>> ExploreAsync(Guid userId, string? query, string? tag, string? cursor,
            int limit, CancellationToken ct = default)
        {
            var packages = _db.WorkflowPackages.AsNoTracking()
                .Where(p => p.Visibility == PackageVisibility.Public && p.LatestVersion > 0);
            if (!string.IsNullOrWhiteSpace(query))
            {
                var pattern = $"%{query.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
                packages = packages.Where(p => EF.Functions.ILike(p.Name, pattern) || EF.Functions.ILike(p.Summary, pattern));
            }
            if (TagOf(tag) is { } wanted)
                packages = packages.Where(p => p.Tags.Contains(wanted));

            if (cursor is not null)
            {
                if (ExploreCursor.Parse(cursor) is not { } after)
                    return Fail<PackagePage>(ErrorCodes.Invalid, "The cursor is not one this list gave.", "cursor");
                // After the last one seen in (installs, updated, id) order, all descending.
                packages = packages.Where(p => p.Installs.Count < after.Installs
                    || (p.Installs.Count == after.Installs
                        && (p.UpdatedAt < after.UpdatedAt || (p.UpdatedAt == after.UpdatedAt && p.Id.CompareTo(after.Id) < 0))));
            }

            // One more than asked: whether there is a next page.
            var views = await ViewsAsync(userId, packages
                .OrderByDescending(p => p.Installs.Count)
                .ThenByDescending(p => p.UpdatedAt)
                .ThenByDescending(p => p.Id)
                .Take(limit + 1), ct);
            var page = views.Take(limit).ToList();
            var next = views.Count > limit && page.Count > 0
                ? new ExploreCursor(page[^1].Installs, page[^1].Package.UpdatedAt, page[^1].Package.Id).ToString()
                : null;
            return ServiceResult<PackagePage>.Success(new PackagePage(page, next));
        }

        // Where an Explore page ended, opaque to clients.
        private sealed record ExploreCursor(int Installs, DateTime UpdatedAt, Guid Id)
        {
            public override string ToString()
                => Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{Installs}|{UpdatedAt.Ticks}|{Id:N}"));

            public static ExploreCursor? Parse(string cursor)
            {
                try
                {
                    var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)).Split('|');
                    return parts.Length == 3
                        && int.TryParse(parts[0], CultureInfo.InvariantCulture, out var installs)
                        && long.TryParse(parts[1], CultureInfo.InvariantCulture, out var ticks)
                        && ticks is >= 0 and <= 3155378975999999999
                        && Guid.TryParseExact(parts[2], "N", out var id)
                        ? new ExploreCursor(installs, new DateTime(ticks, DateTimeKind.Utc), id)
                        : null;
                }
                catch (FormatException)
                {
                    return null;
                }
            }
        }

        public async Task<IServiceResult<PackageView>> GetAsync(Guid userId, Guid id, CancellationToken ct = default)
        {
            var view = await FindAsync(userId, id, tracking: false, ct);
            return view is null ? NotFound<PackageView>() : ServiceResult<PackageView>.Success(view);
        }

        /// <summary>Its versions, newest first, without their content.</summary>
        public async Task<IServiceResult<IReadOnlyList<WorkflowPackageVersion>>> VersionsAsync(Guid userId, Guid id,
            CancellationToken ct = default)
        {
            if (await FindAsync(userId, id, tracking: false, ct) is null)
                return NotFound<IReadOnlyList<WorkflowPackageVersion>>();

            IReadOnlyList<WorkflowPackageVersion> versions = await _db.WorkflowPackageVersions.AsNoTracking()
                .Where(v => v.PackageId == id)
                .OrderByDescending(v => v.Version)
                .Select(v => new WorkflowPackageVersion
                {
                    Id = v.Id,
                    PackageId = v.PackageId,
                    Version = v.Version,
                    Changelog = v.Changelog,
                    ContentHash = v.ContentHash,
                    PublishedBy = v.PublishedBy,
                    CreatedAt = v.CreatedAt,
                })
                .ToListAsync(ct);
            return ServiceResult<IReadOnlyList<WorkflowPackageVersion>>.Success(versions);
        }

        /// <summary>A version with its workflows; null: the latest.</summary>
        public async Task<IServiceResult<PackageVersionDetails>> VersionAsync(Guid userId, Guid id, int? version,
            CancellationToken ct = default)
        {
            var view = await FindAsync(userId, id, tracking: false, ct);
            if (view is null)
                return NotFound<PackageVersionDetails>();

            var number = version ?? view.Package.LatestVersion;
            var saved = await _db.WorkflowPackageVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.PackageId == id && v.Version == number, ct);
            return saved is null
                ? Fail<PackageVersionDetails>(ErrorCodes.NotFound,
                    view.Package.LatestVersion == 0 ? "The package has no version yet." : $"Version {number} not found.")
                : ServiceResult<PackageVersionDetails>.Success(new PackageVersionDetails(saved, WorkflowPackageContent.Parse(saved.Content), view.Package.OwnerId));
        }

        /// <summary>A new package with its first version.</summary>
        public async Task<IServiceResult<PackageView>> CreateAsync(Guid userId, Guid? actorId, PackageInput input,
            PackagePublish publish, CancellationToken ct = default)
        {
            if (Check(input) is { } invalid)
                return Fail<PackageView>(ErrorCodes.Invalid, invalid.Message, invalid.Field);
            var content = await ContentAsync(userId, publish, ct);
            if (!content.Successful)
                return ServiceResult<PackageView>.Failure(content.Error);

            var now = Now();
            var package = new WorkflowPackage { OwnerId = userId, CreatedAt = now };
            Apply(package, input, now);
            _db.WorkflowPackages.Add(package);
            AddVersion(package, content.Value, publish.Changelog, actorId ?? userId, now);
            await _db.SaveChangesAsync(ct);
            package.Owner = await _db.Users.FindAsync([userId], ct);
            return ServiceResult<PackageView>.Success(new PackageView(package, PackageAccess.Owner));
        }

        /// <summary>
        /// A new version from the owner's workflows as they are now. Fails
        /// (409) when it would hold the same as the latest.
        /// </summary>
        public async Task<IServiceResult<PackageVersionDetails>> PublishAsync(Guid userId, Guid? actorId, Guid id,
            PackagePublish publish, CancellationToken ct = default)
        {
            var owned = await OwnedAsync(userId, id, tracking: true, ct);
            if (!owned.Successful)
                return ServiceResult<PackageVersionDetails>.Failure(owned.Error);
            var content = await ContentAsync(userId, publish, ct);
            if (!content.Successful)
                return ServiceResult<PackageVersionDetails>.Failure(content.Error);

            var package = owned.Value.Package;
            var latest = await _db.WorkflowPackageVersions.AsNoTracking()
                .Where(v => v.PackageId == id && v.Version == package.LatestVersion)
                .Select(v => v.ContentHash)
                .FirstOrDefaultAsync(ct);
            if (latest == content.Value.Hash)
                return Fail<PackageVersionDetails>(ErrorCodes.Conflict,
                    $"Nothing changed since version {package.LatestVersion}: its workflows are the same.");

            var version = AddVersion(package, content.Value, publish.Changelog, actorId ?? userId, Now());
            package.UpdatedAt = version.CreatedAt;
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Another publish took the number (unique index) or changed the package.
                return Fail<PackageVersionDetails>(ErrorCodes.Conflict, "The package changed meanwhile; publish again.");
            }
            return ServiceResult<PackageVersionDetails>.Success(new PackageVersionDetails(version, content.Value, package.OwnerId));
        }

        /// <summary>Changes what the package says about itself (not its versions).</summary>
        public async Task<IServiceResult<PackageView>> UpdateAsync(Guid userId, Guid id, PackageInput input, CancellationToken ct = default)
        {
            var owned = await OwnedAsync(userId, id, tracking: true, ct);
            if (!owned.Successful)
                return owned;
            if (Check(input) is { } invalid)
                return Fail<PackageView>(ErrorCodes.Invalid, invalid.Message, invalid.Field);

            var package = owned.Value.Package;
            if (input.ExpectedStamp is { } expected && expected != package.ConcurrencyStamp)
                return Fail<PackageView>(ErrorCodes.Conflict, ChangedMeanwhile);
            Apply(package, input, Now());
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail<PackageView>(ErrorCodes.Conflict, ChangedMeanwhile);
            }
            return owned;
        }

        /// <summary>Deletes the package and its versions; copies already installed stay.</summary>
        public async Task<IServiceResult> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
        {
            var owned = await OwnedAsync(userId, id, tracking: true, ct);
            if (!owned.Successful)
                return ServiceResult.Failure(owned.Error);

            _db.WorkflowPackages.Remove(owned.Value.Package);
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        /// <summary>The users a private package is shared with: only its owner may see them.</summary>
        public async Task<IServiceResult<IReadOnlyList<WorkflowPackageShare>>> SharesAsync(Guid userId, Guid id,
            CancellationToken ct = default)
        {
            var owned = await OwnedAsync(userId, id, tracking: false, ct);
            if (!owned.Successful)
                return ServiceResult<IReadOnlyList<WorkflowPackageShare>>.Failure(owned.Error);

            IReadOnlyList<WorkflowPackageShare> shares = await _db.WorkflowPackageShares.AsNoTracking()
                .Where(s => s.PackageId == id)
                .Include(s => s.User)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync(ct);
            return ServiceResult<IReadOnlyList<WorkflowPackageShare>>.Success(shares);
        }

        /// <summary>Lets a user (by username) read and install the package; sharing twice changes nothing.</summary>
        public async Task<IServiceResult<WorkflowPackageShare>> ShareAsync(Guid userId, Guid id, string userName,
            CancellationToken ct = default)
        {
            var owned = await OwnedAsync(userId, id, tracking: false, ct);
            if (!owned.Successful)
                return ServiceResult<WorkflowPackageShare>.Failure(owned.Error);

            var normalized = (userName ?? string.Empty).Trim().ToUpperInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, ct);
            if (user is null)
                return Fail<WorkflowPackageShare>(ErrorCodes.NotFound, "No user has this username.", "userName");
            if (user.Id == userId)
                return Fail<WorkflowPackageShare>(ErrorCodes.Invalid, "The package is yours already.", "userName");

            var share = await _db.WorkflowPackageShares.FirstOrDefaultAsync(s => s.PackageId == id && s.UserId == user.Id, ct);
            if (share is null)
            {
                share = new WorkflowPackageShare { PackageId = id, UserId = user.Id, CreatedAt = Now() };
                _db.WorkflowPackageShares.Add(share);
                await _db.SaveChangesAsync(ct);
            }
            share.User = user;
            return ServiceResult<WorkflowPackageShare>.Success(share);
        }

        /// <summary>Stops sharing with a user: the owner may, and so may that user (to leave it).</summary>
        public async Task<IServiceResult> UnshareAsync(Guid userId, Guid id, Guid sharedWith, CancellationToken ct = default)
        {
            var view = await FindAsync(userId, id, tracking: false, ct);
            if (view is null)
                return ServiceResult.Failure(new ServiceError(ErrorCodes.NotFound, PackageNotFound));
            if (view.Access != PackageAccess.Owner && sharedWith != userId)
                return ServiceResult.Failure(new ServiceError(ErrorCodes.Forbidden, "Only the package's owner may manage who it is shared with."));

            var share = await _db.WorkflowPackageShares.FirstOrDefaultAsync(s => s.PackageId == id && s.UserId == sharedWith, ct);
            if (share is null)
                return ServiceResult.Failure(new ServiceError(ErrorCodes.NotFound, "The package is not shared with this user."));

            _db.WorkflowPackageShares.Remove(share);
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        /// <summary>
        /// Copies a version's workflows into the user's own, inactive: their
        /// links to each other point at the copies, their connections at the
        /// user's (<paramref name="connections"/>: the package's name to theirs;
        /// a name not given stays when the user has a connection by it), and
        /// every webhook gets a new secret.
        /// </summary>
        public async Task<IServiceResult<PackageInstall>> InstallAsync(Guid userId, Guid? actorId, Guid id, int? version,
            IReadOnlyDictionary<string, string>? connections, CancellationToken ct = default)
        {
            var found = await VersionAsync(userId, id, version, ct);
            if (!found.Successful)
                return ServiceResult<PackageInstall>.Failure(found.Error);
            var content = found.Value.Content;

            var mine = await _db.Connections.AsNoTracking()
                .Where(c => c.UserId == userId)
                .Select(c => new { c.Name, c.Provider })
                .ToListAsync(ct);
            bool Has(string name, string? provider) => mine.Any(c => c.Name == name && (provider is null || c.Provider == provider));

            var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
            var missing = new List<PackageConnection>();
            foreach (var needed in content.Connections)
            {
                if (connections?.GetValueOrDefault(needed.Name) is { Length: > 0 } chosen)
                {
                    if (!Has(chosen, needed.Provider))
                        return Fail<PackageInstall>(ErrorCodes.Invalid,
                            $"You have no {needed.Provider ?? "such"} connection named \"{chosen}\".", "connections");
                    renamed[needed.Name] = chosen;
                }
                else if (!Has(needed.Name, needed.Provider))
                    missing.Add(needed);
            }

            var ids = content.Workflows.ToDictionary(w => w.Key, _ => Uuid.NewDatabaseFriendly(Database.PostgreSql));
            var rewrite = new LinkRewrite(ids, renamed, NewSecret);
            var items = content.Workflows
                .Select(w => new NewWorkflow(ids[w.Key], w.Name, WorkflowLinks.Rewrite(w.Program, rewrite),
                    (id, found.Value.Version.Version, w.Key)))
                .ToList();

            var created = await _workflows.CreateManyAsync(userId, actorId, items, ct);
            if (!created.Successful)
                return ServiceResult<PackageInstall>.Failure(created.Error);

            // Others' installs count, once each; a row of its own never conflicts with an edit of the package.
            if (found.Value.OwnerId != userId)
            {
                var install = await _db.WorkflowPackageInstalls.FirstOrDefaultAsync(i => i.PackageId == id && i.UserId == userId, ct);
                if (install is null)
                    _db.WorkflowPackageInstalls.Add(install = new WorkflowPackageInstall { PackageId = id, UserId = userId });
                install.Version = found.Value.Version.Version;
                install.InstalledAt = Now();
                await _db.SaveChangesAsync(ct);
            }

            return ServiceResult<PackageInstall>.Success(new PackageInstall(found.Value, created.Value, missing));
        }

        // The publish's workflows as a package version's content.
        private async Task<IServiceResult<WorkflowPackageContent>> ContentAsync(Guid userId, PackagePublish publish, CancellationToken ct)
        {
            var wanted = publish.Workflows.Distinct().ToList();
            if (wanted.Count > MaxWorkflows)
                return Fail<WorkflowPackageContent>(ErrorCodes.Invalid, $"A package holds at most {MaxWorkflows} workflows.", "workflows");
            if ((publish.Changelog ?? string.Empty).Length > WorkflowPackageVersionConfiguration.MaxChangelogLength)
                return Fail<WorkflowPackageContent>(ErrorCodes.Invalid, "The changelog is too long.", "changelog");

            var workflows = await _db.Workflows.AsNoTracking()
                .Where(w => w.UserId == userId && wanted.Contains(w.Id))
                .ToListAsync(ct);
            if (wanted.FirstOrDefault(id => workflows.All(w => w.Id != id)) is var unknown && unknown != Guid.Empty)
                return Fail<WorkflowPackageContent>(ErrorCodes.NotFound, $"Workflow {unknown} not found.", "workflows");

            var providers = await _db.Connections.AsNoTracking()
                .Where(c => c.UserId == userId)
                .Select(c => new { c.Name, c.Provider })
                .ToListAsync(ct);
            string? ProviderOf(string name)
            {
                var matching = providers.Where(c => c.Name == name).Select(c => c.Provider).Distinct().ToList();
                return matching.Count == 1 ? matching[0] : null;
            }

            // In the order given: the first is the main one unless named.
            var sources = wanted.Select(id => workflows.First(w => w.Id == id))
                .Select(w => new PackageSource(w.Id, w.Name, WorkflowService.Parse(w)))
                .ToList();
            var (content, problems) = WorkflowPackageContent.Build(sources, publish.Main, ProviderOf);
            return content is null
                ? Fail<WorkflowPackageContent>(ErrorCodes.Invalid, string.Join(" ", problems), "workflows")
                : ServiceResult<WorkflowPackageContent>.Success(content);
        }

        private WorkflowPackageVersion AddVersion(WorkflowPackage package, WorkflowPackageContent content, string? changelog,
            Guid publishedBy, DateTime now)
        {
            package.LatestVersion++;
            var version = new WorkflowPackageVersion
            {
                PackageId = package.Id,
                Version = package.LatestVersion,
                Changelog = changelog?.Trim() ?? string.Empty,
                ContentHash = content.Hash,
                Content = content.ToJson(),
                PublishedBy = publishedBy,
                CreatedAt = now,
            };
            _db.WorkflowPackageVersions.Add(version);
            return version;
        }

        private static void Apply(WorkflowPackage package, PackageInput input, DateTime now)
        {
            package.Name = input.Name.Trim();
            package.Summary = input.Summary?.Trim() ?? string.Empty;
            package.Readme = input.Readme ?? string.Empty;
            package.Visibility = input.Visibility;
            package.Tags = (input.Tags ?? []).Select(TagOf).OfType<string>().Distinct().ToArray();
            package.UpdatedAt = now;
        }

        // Which input cannot be a package's and why, or null.
        private static (string Field, string Message)? Check(PackageInput input)
        {
            var name = input.Name?.Trim() ?? string.Empty;
            if (name.Length is 0 or > WorkflowPackageConfiguration.MaxNameLength)
                return ("name", $"The name must be 1-{WorkflowPackageConfiguration.MaxNameLength} characters.");
            if ((input.Summary ?? string.Empty).Trim().Length > WorkflowPackageConfiguration.MaxSummaryLength)
                return ("summary", $"The summary may be at most {WorkflowPackageConfiguration.MaxSummaryLength} characters.");
            if ((input.Readme ?? string.Empty).Length > WorkflowPackageConfiguration.MaxReadmeLength)
                return ("readme", "The README is too long.");
            if (!Enum.IsDefined(input.Visibility))
                return ("visibility", "Unknown visibility.");
            var tags = input.Tags ?? [];
            if (tags.Count > WorkflowPackageConfiguration.MaxTags)
                return ("tags", $"At most {WorkflowPackageConfiguration.MaxTags} tags.");
            if (tags.FirstOrDefault(t => TagOf(t) is null) is { } bad)
                return ("tags", $"\"{bad}\" is not a tag: up to {WorkflowPackageConfiguration.MaxTagLength} letters, digits and dashes.");
            return null;
        }

        /// <summary>A tag as stored: lower case, spaces as dashes; null when it cannot be one.</summary>
        public static string? TagOf(string? tag)
        {
            var normalized = string.Join('-', (tag ?? string.Empty).Trim().ToLowerInvariant()
                .Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries));
            return normalized.Length is > 0 and <= WorkflowPackageConfiguration.MaxTagLength
                && normalized.All(c => char.IsLetterOrDigit(c) || c == '-')
                ? normalized
                : null;
        }

        private static string NewSecret()
            => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_');

        private async Task<PackageView?> FindAsync(Guid userId, Guid id, bool tracking, CancellationToken ct)
        {
            var packages = tracking ? _db.WorkflowPackages : _db.WorkflowPackages.AsNoTracking();
            var views = await ViewsAsync(userId, packages
                .Where(p => p.Id == id && (p.OwnerId == userId || p.Visibility != PackageVisibility.Private
                    || p.Shares.Any(s => s.UserId == userId))), ct);
            return views.FirstOrDefault();
        }

        // With their owners and install counts, in the query's order.
        private static async Task<IReadOnlyList<PackageView>> ViewsAsync(Guid userId, IQueryable<WorkflowPackage> packages,
            CancellationToken ct)
        {
            var rows = await packages
                .Select(p => new { Package = p, p.Owner, Installs = p.Installs.Count })
                .ToListAsync(ct);
            return rows.Select(r =>
            {
                r.Package.Owner = r.Owner;
                return new PackageView(r.Package, AccessOf(r.Package, userId), r.Installs);
            }).ToList();
        }

        private async Task<IServiceResult<PackageView>> OwnedAsync(Guid userId, Guid id, bool tracking, CancellationToken ct)
        {
            var view = await FindAsync(userId, id, tracking, ct);
            if (view is null)
                return NotFound<PackageView>();
            return view.Access == PackageAccess.Owner
                ? ServiceResult<PackageView>.Success(view)
                : Fail<PackageView>(ErrorCodes.Forbidden, "Only the package's owner may change it.");
        }

        private static PackageAccess AccessOf(WorkflowPackage package, Guid userId)
            => package.OwnerId == userId ? PackageAccess.Owner : PackageAccess.Read;

        private const string PackageNotFound = "Package not found.";
        private const string ChangedMeanwhile = "The package changed meanwhile; open it again to see the change.";

        private DateTime Now() => _time.GetUtcNow().UtcDateTime;

        private static IServiceResult<T> Fail<T>(string code, string message, string? field = null)
            => ServiceResult<T>.Failure(new ServiceError(code, message, field));

        private static IServiceResult<T> NotFound<T>() => Fail<T>(ErrorCodes.NotFound, PackageNotFound);
    }
}
