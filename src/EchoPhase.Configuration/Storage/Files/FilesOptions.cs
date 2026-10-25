// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Storage.Files
{
    /// <summary>Limits of the files users keep in object storage.</summary>
    public class FilesOptions : IValidatable
    {
        public const string SectionName = "Storage:Files";

        /// <summary>The most an upload may be: the ceiling of every limit below.</summary>
        public const long MaxUploadBytes = 1L << 30;

        /// <summary>An avatar's limit.</summary>
        public long AvatarMaxBytes { get; set; } = 5L << 20;

        /// <summary>Any other file's limit.</summary>
        public long MaxFileBytes { get; set; } = 100L << 20;

        /// <summary>The total of a user's files.</summary>
        public long QuotaBytes { get; set; } = 1L << 30;

        /// <summary>How long an upload may stay unfinished before prune removes it.</summary>
        public TimeSpan PendingTimeout { get; set; } = TimeSpan.FromDays(1);

        public IValidationResult Validate()
        {
            if (AvatarMaxBytes < 1 || AvatarMaxBytes > MaxUploadBytes)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(AvatarMaxBytes), $"AvatarMaxBytes must be between 1 and {MaxUploadBytes}."));

            if (MaxFileBytes < 1 || MaxFileBytes > MaxUploadBytes)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(MaxFileBytes), $"MaxFileBytes must be between 1 and {MaxUploadBytes}."));

            if (QuotaBytes < 1)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(QuotaBytes), "QuotaBytes must be positive."));

            if (PendingTimeout <= TimeSpan.Zero)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(PendingTimeout), "PendingTimeout must be positive."));

            return ValidationResult.Success();
        }
    }

    public class FilesValidator : ValidatorBase<FilesOptions>
    {
    }
}
