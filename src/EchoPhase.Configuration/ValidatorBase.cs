// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.Options;

namespace EchoPhase.Configuration
{
    public abstract class ValidatorBase<T> : IValidateOptions<T> where T : class
    {
        public ValidateOptionsResult Validate(string? name, T options)
        {
            if (options is not IValidatable settings)
                return ValidateOptionsResult.Fail($"Non-validatable settings model. Implement IValidatable interface for {options.GetType().Name}.");

            var result = settings.Validate();
            if (!result.Successful)
                return ValidateOptionsResult.Fail($"{options.GetType().Name}: {result.Error.Value}");

            return AdditionalValidation(name, options);
        }

        protected virtual ValidateOptionsResult AdditionalValidation(string? name, T options) =>
            ValidateOptionsResult.Success;
    }
}
