// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Notifications
{
    /// <summary>What to tell a user.</summary>
    /// <param name="Kind">One of <see cref="NotificationKinds"/>.</param>
    /// <param name="Title">In English: the fallback for clients that do not know the kind.</param>
    /// <param name="Body">More text, or empty.</param>
    /// <param name="Link">A path in the app to open, or empty.</param>
    /// <param name="Params">The values a client words the kind with.</param>
    public sealed record NotificationMessage(
        string Kind,
        string Title,
        string Body = "",
        string Link = "",
        IReadOnlyDictionary<string, string>? Params = null)
    {
        public const int MaxTitleLength = 200;
        public const int MaxBodyLength = 2000;
        public const int MaxLinkLength = 500;
    }
}
