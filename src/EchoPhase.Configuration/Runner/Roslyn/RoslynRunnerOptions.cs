// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Runner.Roslyn
{
    public class RoslynOptions : IValidatable
    {
        public const string SectionName = "Runner:Roslyn";
        public ISet<string>? Import { get; set; } = null;
        public ISet<string>? Allow { get; set; } = null;

        public IValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
