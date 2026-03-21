// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Types.Validation
{
    /// <summary>
    /// Defines the result of a validation operation.
    /// </summary>
    public interface IValidationResult
    {
        /// <summary>
        /// Indicates whether the validation was successful.
        /// </summary>
        bool Successful
        {
            get;
        }

        /// <summary>
        /// The error associated with the validation, if any.
        /// </summary>
        IValidationError Error
        {
            get;
        }

        /// <summary>
        /// Sets the error and marks the result as failed.
        /// </summary>
        IValidationResult SetError(Action<ValidationError> configure);

        IValidationResult Then(Func<IValidationResult> next);
    }
}
