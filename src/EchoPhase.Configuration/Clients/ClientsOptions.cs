// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Clients
{
    public class ClientsOptions : IValidatable
    {
        public const string SectionName = "Clients";

        public IValidationResult Validate()
        {
            return ValidateSelf();
        }

        private IValidationResult ValidateSelf()
        {
            return ValidationResult.Success();
        }
    }
}
