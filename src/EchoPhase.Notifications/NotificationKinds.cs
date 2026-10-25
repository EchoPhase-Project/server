// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Notifications
{
    /// <summary>
    /// What notifications are about. Clients word a known kind themselves
    /// (from its params) and fall back to the title and body for others, so
    /// more kinds may come.
    /// </summary>
    public static class NotificationKinds
    {
        /// <summary>Sent by a workflow (the Notify block): its own title and text.</summary>
        public const string Workflow = "workflow";

        /// <summary>Someone signed in to the account. Params: device.</summary>
        public const string SignIn = "sign_in";

        /// <summary>Files take most of the quota. Params: percent.</summary>
        public const string StorageQuota = "storage_quota";

        /// <summary>A note was shared with the user. Params: note, by, access.</summary>
        public const string NoteShared = "note_shared";

        /// <summary>A note's reminder is due. Params: note.</summary>
        public const string NoteReminder = "note_reminder";
    }
}
