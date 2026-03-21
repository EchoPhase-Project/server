// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Argon2
{
    public class Argon2Options : IValidatable
    {
        public const string SectionName = "Argon2";
        public string Key { get; set; } = "argon2";

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Key))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Key), "Key cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
