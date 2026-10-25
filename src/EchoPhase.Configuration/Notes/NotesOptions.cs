// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Notes
{
    /// <summary>Users' notes: how often reminders are looked for.</summary>
    public class NotesOptions : IValidatable
    {
        public const string SectionName = "Notes";

        /// <summary>Due reminders are sent within this long of their time.</summary>
        public TimeSpan ReminderInterval { get; set; } = TimeSpan.FromMinutes(1);

        public IValidationResult Validate()
        {
            if (ReminderInterval < TimeSpan.FromSeconds(5))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ReminderInterval), "ReminderInterval must be at least 5 seconds."));

            return ValidationResult.Success();
        }
    }

    public class NotesValidator : ValidatorBase<NotesOptions>
    {
    }
}
