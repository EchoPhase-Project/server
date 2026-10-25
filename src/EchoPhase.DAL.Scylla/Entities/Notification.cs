// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Entities
{
    /// <summary>
    /// Something a user is told about: kept in their inbox (their partition,
    /// newest first) until it expires, so what they missed while away is
    /// there when they come back.
    /// </summary>
    public class Notification
    {
        public Guid UserId { get; set; }

        /// <summary>UUIDv7: byte order is creation order, newest first in the table.</summary>
        public Guid Id { get; set; }

        /// <summary>What it is about (workflow, sign_in, ...): clients word it by kind.</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>In English; the fallback for a kind the client has no words for.</summary>
        public string Title { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        /// <summary>A path in the app to open (/notes/…), or empty.</summary>
        public string Link { get; set; } = string.Empty;

        /// <summary>The kind's values (JSON object of strings), for the client's wording.</summary>
        public string Params { get; set; } = "{}";

        public bool Read { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
