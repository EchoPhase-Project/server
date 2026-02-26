namespace EchoPhase.Types.Validation
{
    /// <summary>
    /// Defines a validation error with type, description, and message.
    /// </summary>
    public interface IValidationError
    {
        List<string> Prefixes
        {
            get;
        }

        /// <summary>
        /// Gets the human-readable message describing the validation error.
        /// </summary>
        string Message
        {
            get;
        }

        /// <summary>
        /// Gets the string representation of the error.
        /// </summary>
        string Value
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether this error has null or whitespace values.
        /// </summary>
        bool IsNullOrEmpty
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether this error contains non-empty values.
        /// </summary>
        bool HasValue
        {
            get;
        }

        /// <summary>
        /// Applies the specified configuration action to this error instance.
        /// </summary>
        IValidationError Configure(Action<ValidationError> configure);
    }
}
