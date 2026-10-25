// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Notifications
{
    /// <summary>How long users' notifications are kept, and how many are shown.</summary>
    public class NotificationsOptions : IValidatable
    {
        public const string SectionName = "Notifications";

        /// <summary>A notification expires this long after it was sent, read or not.</summary>
        public TimeSpan Ttl { get; set; } = TimeSpan.FromDays(30);

        /// <summary>The most recent this many make the inbox (and its unread count).</summary>
        public int InboxSize { get; set; } = 200;

        public IValidationResult Validate()
        {
            if (Ttl < TimeSpan.FromMinutes(1) || Ttl.TotalSeconds > int.MaxValue)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Ttl), "Ttl must be at least a minute (and fit in seconds as an int)."));

            if (InboxSize is < 1 or > 1000)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(InboxSize), "InboxSize must be between 1 and 1000."));

            return ValidationResult.Success();
        }
    }

    public class NotificationsValidator : ValidatorBase<NotificationsOptions>
    {
    }
}
