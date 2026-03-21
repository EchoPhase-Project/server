// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

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

        public string? ProfileImageName { get; set; } = default;

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<ExternalToken> ExternalTokens { get; set; } = new List<ExternalToken>();

        public ICollection<WebHook> WebHooks { get; set; } = new List<WebHook>();

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
