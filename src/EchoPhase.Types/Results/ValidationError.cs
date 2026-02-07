namespace EchoPhase.Types.Results
{
    /// <summary>
    /// Represents a validation error with a type, description, and message.
    /// </summary>
	public class ValidationError : IValidationError
    {
        /// <summary>
        /// Gets the list of prefixes representing the validation path (from root to specific property).
        /// </summary>
        public List<string> Prefixes { get; private set; } = new();

        /// <summary>
        /// Gets the human-readable message describing the validation error.
        /// </summary>
        public string Message { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the full issuer path built from prefixes.
        /// </summary>
        public string Issuer =>
            Prefixes.Any() ? string.Join(".", Prefixes) : string.Empty;

        /// <summary>
        /// Gets the string representation of the error.
        /// </summary>
        public string Value =>
            ToString();

        /// <summary>
        /// Gets a value indicating whether this error has null or whitespace values.
        /// </summary>
        public bool IsNullOrEmpty =>
            !Prefixes.Any() || string.IsNullOrWhiteSpace(Message);

        /// <summary>
        /// Gets a value indicating whether this error contains non-empty values.
        /// </summary>
        public bool HasValue =>
            !IsNullOrEmpty;

        /// <summary>
        /// Gets a default, empty instance of <see cref="ValidationError"/>.
        /// </summary>
        public static IValidationError Default =>
            new ValidationError();

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationError"/> class.
        /// </summary>
        public ValidationError()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationError"/> class, configured by the specified action.
        /// </summary>
        /// <param name="configure">An action to configure this error instance.</param>
        public ValidationError(Action<ValidationError> configure)
        {
            configure.Invoke(this);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationError"/> class with the specified type, prefixes, and message.
        /// </summary>
        /// <param name="prefixes">The list of prefixes representing the path.</param>
        /// <param name="message">The error message.</param>
        public ValidationError(IEnumerable<string> prefixes, string message)
        {
            Prefixes = prefixes?.ToList() ?? new List<string>();
            Message = message;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationError"/> class with a single prefix.
        /// </summary>
        /// <param name="prefix">A single prefix.</param>
        /// <param name="message">The error message.</param>
        public ValidationError(string prefix, string message)
        {
            Prefixes = new List<string> { prefix };
            Message = message;
        }

        /// <summary>
        /// Applies the specified configuration action to this error instance.
        /// </summary>
        /// <param name="configure">An action that modifies this error instance.</param>
        /// <returns>The current <see cref="IValidationError"/> instance.</returns>
        public IValidationError Configure(Action<ValidationError> configure)
        {
            configure.Invoke(this);
            return this;
        }

        /// <summary>
        /// Returns a string representation of this error.
        /// </summary>
        /// <returns>
        /// A string in the format "Type - Description: Message", or an empty string if any field is null or whitespace.
        /// </returns>
        public override string ToString() =>
            IsNullOrEmpty ? string.Empty : $"{Issuer}: {Message}";

        /// <summary>
        /// Adds a prefix to the end of the prefix list.
        /// </summary>
        /// <param name="prefix">The prefix to add.</param>
        public void AddPrefix(string prefix)
        {
            if (!string.IsNullOrWhiteSpace(prefix))
                Prefixes.Add(prefix);
        }

        /// <summary>
        /// Adds multiple prefixes to the end of the prefix list.
        /// </summary>
        /// <param name="prefixes">The prefixes to add.</param>
        public void AddPrefixes(IEnumerable<string> prefixes)
        {
            if (prefixes != null)
                Prefixes.AddRange(prefixes.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        /// <summary>
        /// Sets the prefix list, replacing any existing prefixes.
        /// </summary>
        /// <param name="prefixes">The prefixes to set.</param>
        public void SetPrefixes(IEnumerable<string> prefixes) =>
            Prefixes = prefixes?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();

        /// <summary>
        /// Sets a single prefix, replacing any existing prefixes.
        /// </summary>
        /// <param name="prefix">The prefix to set.</param>
        public void SetPrefix(string prefix) =>
            Prefixes = string.IsNullOrWhiteSpace(prefix) ? new List<string>() : new List<string> { prefix };

        /// <summary>
        /// Sets the error message.
        /// </summary>
        /// <param name="message">The error message to set. If null, sets to an empty string.</param>
        public void SetMessage(string message) =>
            Message = message ?? string.Empty;

        /// <summary>
        /// Sets the type, a single prefix, and message.
        /// </summary>
        /// <param name="prefix">A single prefix.</param>
        /// <param name="message">The error message.</param>
        public void Set(string prefix, string message)
        {
            SetPrefix(prefix);
            SetMessage(message);
        }

        /// <summary>
        /// Creates a copy of this error with an additional prefix prepended to the path.
        /// </summary>
        /// <param name="prefix">The prefix to prepend.</param>
        /// <returns>A new ValidationError with the prefix added at the beginning.</returns>
        public ValidationError WithPrefix(string prefix)
        {
            var newPrefixes = new List<string>();

            if (!string.IsNullOrWhiteSpace(prefix))
                newPrefixes.Add(prefix);

            newPrefixes.AddRange(Prefixes);

            return new ValidationError(newPrefixes, Message);
        }
    }
}
