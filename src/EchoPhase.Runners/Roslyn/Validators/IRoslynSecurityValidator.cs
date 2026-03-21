// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Roslyn.Validators
{
    public interface ISecurityValidator
    {
        /// <summary>
        /// Validates code for allowed assemblies. Returs validation errors.
        /// </summary>
        IEnumerable<string> Validate(string code);
    }
}
