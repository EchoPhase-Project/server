// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Cryptography.Aes
{
    public class AesOptions : IValidatable
    {
        public const string SectionName = "Cryptography:Aes";
        public int TagSize { get; set; } = 16;
        public int NonceSize { get; set; } = 12;
        public string Key { get; set; } = "aes-gcm";

        public IValidationResult Validate()
        {
            if (TagSize <= 0)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(TagSize), "TagSize must be positive."));

            if (NonceSize <= 0)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(NonceSize), "NonceSize must be positive."));

            if (string.IsNullOrWhiteSpace(Key))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Key), "Key cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
