// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;
using EchoPhase.DAL.Abstractions;
using UUIDNext;

namespace EchoPhase.DAL.Postgres.Models
{
    /// <summary>
    /// A file in object storage: what it is and whose; its bytes are under
    /// <see cref="StorageKey"/> in the bucket. Goes with its owner.
    /// </summary>
    public class StoredFile : ITrackingEntity, IIdentifiable
    {
        public Guid Id { get; set; } = Uuid.NewDatabaseFriendly(Database.PostgreSql);

        public Guid OwnerId { get; set; }
        public User? Owner { get; set; }

        /// <summary>What it is for; decides its limits and who may read it.</summary>
        public FilePurpose Purpose { get; set; }

        /// <summary>The name it was uploaded with, as given (shown, never used as a path).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The type found in its content, not the one the uploader claimed.</summary>
        public string ContentType { get; set; } = "application/octet-stream";

        public long Size { get; set; }

        /// <summary>Hex SHA-256 of the content; empty until the upload completes.</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>Its key in the bucket: files/{Id}, nothing user-chosen.</summary>
        public string StorageKey { get; set; } = string.Empty;

        public FileStatus Status { get; set; } = FileStatus.Pending;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [JsonConverter(typeof(JsonStringEnumConverter<FilePurpose>))]
    public enum FilePurpose
    {
        /// <summary>A user's picture: any signed-in user may see it.</summary>
        Avatar,
        /// <summary>Data to import (workflows, settings): its owner's only.</summary>
        Import,
        /// <summary>Something a workflow produced: its owner's only.</summary>
        Artifact,
        /// <summary>Anything else a user keeps: its owner's only.</summary>
        Attachment,
    }

    public enum FileStatus
    {
        /// <summary>Being uploaded; prune removes it if the upload never completes.</summary>
        Pending,
        Ready,
    }
}
