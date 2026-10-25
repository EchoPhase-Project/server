// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EchoPhase.DAL.Abstractions;
using EchoPhase.Projection.Attributes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    [Comment("Authorised User Model")]
    public class User : IdentityUser<Guid>, ITrackingEntity, IDisposable, IIdentifiable
    {
        [Expose] public override Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);
        [Expose] public override string? UserName { get; set; }
        [Expose] public string Name { get; set; }

        /// <summary>The user's picture (a file of purpose Avatar); null: none.</summary>
        [Expose] public Guid? AvatarFileId { get; set; }
        public StoredFile? AvatarFile { get; set; }

        /// <summary>
        /// IANA time zone (e.g. "Europe/Moscow") for showing and, later,
        /// scheduling in the user's local time. Times are stored in UTC
        /// regardless; this is a preference, not part of any timestamp.
        /// </summary>
        [Expose][MaxLength(64)] public string TimeZone { get; set; } = "UTC";

        public ICollection<Session> Sessions { get; set; } = new List<Session>();
        public ICollection<ApiClient> ApiClients { get; set; } = new List<ApiClient>();
        public ICollection<Connection> Connections { get; set; } = new List<Connection>();
        public ICollection<OAuthApp> OAuthApps { get; set; } = new List<OAuthApp>();

        public ICollection<WebHook> WebHooks { get; set; } = new List<WebHook>();
        public ICollection<Workflow> Workflows { get; set; } = new List<Workflow>();

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User(string name)
        {
            Name = name;
        }

        [NotMapped]
        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
                _disposed = true;
        }

        ~User() =>
            Dispose(false);
    }
}
