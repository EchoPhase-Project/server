using EchoPhase.Interfaces;
using Microsoft.Extensions.Options;

namespace EchoPhase.Validators
{
    public abstract class ValidatorBase<T> : IValidateOptions<T> where T : class
    {
        public ValidateOptionsResult Validate(string? name, T options)
        {
            if (options is not IValidatable settings)
                return ValidateOptionsResult.Fail($"Non-validatable settings model. Implement IValidatable interface for {options.GetType().Name}.");

            if (!settings.IsValid(out string errorMessage))
                return ValidateOptionsResult.Fail($"{options.GetType().Name}: {errorMessage}");

            return AdditionalValidation(name, options);
        }

        protected virtual ValidateOptionsResult AdditionalValidation(string? name, T options) =>
            ValidateOptionsResult.Success;
    }
}
