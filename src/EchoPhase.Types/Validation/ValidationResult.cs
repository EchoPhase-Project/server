namespace EchoPhase.Types.Validation
{
    /// <summary>
    /// Represents the result of a validation operation, indicating success or failure and optionally providing an error.
    /// </summary>
    public class ValidationResult : IValidationResult
    {
        /// <summary>
        /// Indicates whether the validation was successful.
        /// </summary>
        public bool Successful { get; private set; } = true;

        /// <summary>
        /// The error associated with the validation, if any.
        /// </summary>
        public IValidationError Error { get; private set; } = ValidationError.Default;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationResult"/> class with the specified success status.
        /// </summary>
        /// <param name="success">Whether the validation was successful.</param>
        public ValidationResult(bool success)
        {
            Successful = success;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationResult"/> class with a success flag and an error configured via callback.
        /// </summary>
        /// <param name="success">Whether the validation was successful.</param>
        /// <param name="configure">An action to configure the error object.</param>
        public ValidationResult(bool success, Action<ValidationError> configure) : this(success)
        {
            Error = new ValidationError(configure);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationResult"/> class with a success flag and an existing error object.
        /// </summary>
        /// <param name="success">Whether the validation was successful.</param>
        /// <param name="error">The error to associate with this result.</param>
        public ValidationResult(bool success, IValidationError error) : this(success)
        {
            Error = error;
        }

        /// <summary>
        /// Sets the error and marks the result as failed.
        /// </summary>
        /// <param name="configure">An action to configure the error object.</param>
        /// <returns>The current instance.</returns>
        public IValidationResult SetError(Action<ValidationError> configure)
        {
            Successful = false;
            Error.Configure(configure);
            return this;
        }

        /// <summary>
        /// Creates a successful <see cref="ValidationResult"/>.
        /// </summary>
        public static IValidationResult Success() =>
            new ValidationResult(true);

        public IValidationResult Then(Func<IValidationResult> next)
        {
            if (!Successful)
                return this;

            return next();
        }

        /// <summary>
        /// Creates a failed <see cref="ValidationResult"/> with a configured error.
        /// </summary>
        public static IValidationResult Failure(Action<ValidationError> configure) =>
            new ValidationResult(false, configure);

        /// <summary>
        /// Creates a failed <see cref="ValidationResult"/> with the specified error.
        /// </summary>
        public static IValidationResult Failure(IValidationError error) =>
            new ValidationResult(false, error);
    }
}
