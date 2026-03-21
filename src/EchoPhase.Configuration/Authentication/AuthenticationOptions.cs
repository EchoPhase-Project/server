// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Validation.Extensions;

namespace EchoPhase.Configuration.Authentication
{
    public class AuthenticationOptions : IValidatable
    {
        public const string SectionName = "Authentication";
        public Bearer.BearerOptions Bearer { get; set; } = new();
        public Refresh.RefreshOptions Refresh { get; set; } = new();

        public IValidationResult Validate()
        {
            return Bearer.Validate().WithPrefix(nameof(Bearer))
                .Then(() => Refresh.Validate().WithPrefix(nameof(Refresh)))
                .Then(() => ValidateSelf());
        }

        private IValidationResult ValidateSelf()
        {
            return ValidationResult.Success();
        }
    }
}
