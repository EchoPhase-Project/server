// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Types.Validation.Extensions
{
    public static class ValidationResultExtensions
    {
        /// <summary>
        /// Adds a prefix to the validation error path.
        /// </summary>
        /// <param name="result">The validation result.</param>
        /// <param name="prefix">The prefix to add.</param>
        /// <returns>A new validation result with the prefixed error.</returns>
        public static IValidationResult WithPrefix(this IValidationResult result, string prefix)
        {
            if (result.Successful)
                return result;

            var newError = ((ValidationError)result.Error).WithPrefix(prefix);
            return ValidationResult.Failure(newError);
        }
    }
}
