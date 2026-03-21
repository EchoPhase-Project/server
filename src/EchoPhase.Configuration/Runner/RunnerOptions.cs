// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Validation.Extensions;

namespace EchoPhase.Configuration.Runner
{
    public class RunnerOptions : IValidatable
    {
        public const string SectionName = "Runner";
        public Roslyn.RoslynOptions Roslyn { get; set; } = new();

        public IValidationResult Validate()
        {
            return Roslyn.Validate().WithPrefix(nameof(Roslyn))
                .Then(() => ValidateSelf());
        }

        private IValidationResult ValidateSelf()
        {
            return ValidationResult.Success();
        }
    }
}
