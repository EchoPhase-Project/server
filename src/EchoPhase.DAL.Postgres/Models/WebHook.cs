// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using EchoPhase.DAL.Abstractions;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    public class WebHook : ITrackingEntity, IConcurrentEntity, IIdentifiable
    {
        public Guid Id
        {
            get; set;
        } = Uuid.NewDatabaseFriendly(Database.PostgreSql);

        public WebHookStatus Status { get; set; } = WebHookStatus.Enabled;

        public Guid UserId
        {
            get;
            set
            {
                if (value == Guid.Empty)
                    throw new InvalidOperationException("Guid cannot be empty.");

                field = value;
            }
        }

        public string Url
        {
            get;
            set
            {
                if (string.IsNullOrWhiteSpace(value) || !IsValidUrl(value))
                    throw new InvalidOperationException("Invalid URL.");

                field = value;
            }
        } = string.Empty;

        public User? User
        {
            get; set;
        }

        public string Name { get; set; } = string.Empty;

        public string Intents { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ConcurrencyCheck]
        public Guid ConcurrencyStamp { get; set; } = Uuid.NewRandom();

        private bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                   && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
    }
}
