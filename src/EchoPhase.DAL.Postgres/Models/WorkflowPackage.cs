// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;
using EchoPhase.DAL.Abstractions;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>Who may see and install a package.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PackageVisibility>))]
    public enum PackageVisibility
    {
        /// <summary>Its owner and the users it is shared with.</summary>
        Private,
        /// <summary>Anyone signed in who has its link; not listed.</summary>
        Unlisted,
        /// <summary>Anyone signed in; listed in Explore.</summary>
        Public,
    }

    /// <summary>
    /// Workflows published together, with a README: others read them and
    /// install copies (never edit the author's). Each publish is a fixed
    /// <see cref="WorkflowPackageVersion"/>; the package itself holds what
    /// may change without one (name, text, tags, who may see it).
    /// </summary>
    public class WorkflowPackage : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);

        public Guid OwnerId { get; set; }
        public User? Owner { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>One or two sentences for its card.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>Markdown, rendered by the client (raw HTML is not).</summary>
        public string Readme { get; set; } = string.Empty;

        public PackageVisibility Visibility { get; set; }

        /// <summary>The author's tags: lower case, letters, digits and dashes.</summary>
        public string[] Tags { get; set; } = [];

        /// <summary>The newest version's number; 0 before the first is published.</summary>
        public int LatestVersion { get; set; }

        public List<WorkflowPackageVersion> Versions { get; set; } = [];
        public List<WorkflowPackageShare> Shares { get; set; } = [];

        /// <summary>Who installed it (its owner aside): one each, however often.</summary>
        public List<WorkflowPackageInstall> Installs { get; set; } = [];

        public Guid ConcurrencyStamp { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A package as one publish left it: its workflows and the connections they need. Never changes.</summary>
    public class WorkflowPackageVersion
    {
        public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        public Guid PackageId { get; set; }
        public WorkflowPackage? Package { get; set; }

        /// <summary>1, 2, … in publish order.</summary>
        public int Version { get; set; }

        /// <summary>What changed, Markdown.</summary>
        public string Changelog { get; set; } = string.Empty;

        /// <summary>SHA-256 (hex) of the content (WorkflowPackageContent.Hash).</summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>The workflows and connections as JSON (jsonb): WorkflowPackageContent.</summary>
        public string Content { get; set; } = "{}";

        /// <summary>Who published it; differs from the owner under impersonation.</summary>
        public Guid? PublishedBy { get; set; }

        /// <summary>UTC.</summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>A private package shared with a user: to read and install.</summary>
    public class WorkflowPackageShare
    {
        public Guid PackageId { get; set; }
        public WorkflowPackage? Package { get; set; }

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A user who installed a package (not its owner), and when they last did.</summary>
    public class WorkflowPackageInstall
    {
        public Guid PackageId { get; set; }
        public WorkflowPackage? Package { get; set; }

        public Guid UserId { get; set; }
        public User? User { get; set; }

        /// <summary>The version they installed last.</summary>
        public int Version { get; set; }

        /// <summary>UTC.</summary>
        public DateTime InstalledAt { get; set; }
    }
}
