namespace EchoPhase.Types.Results
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
