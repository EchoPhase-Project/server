// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Types.Result
{
    /// <summary>
    /// Represents an error object used to describe service operation errors with a code and message.
    /// Set while it is being built (the constructor's configure action), read-only afterwards.
    /// </summary>
    public class ServiceError : IServiceError
    {
        private bool _built;

        /// <summary>
        /// Gets the error code representing the type or category of the error.
        /// </summary>
        public string Code { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the human-readable message describing the error.
        /// </summary>
        public string Message { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the string representation of the error, typically combining the code and message.
        /// </summary>
        public string Value =>
            ToString();

        /// <summary>
        /// Gets a value indicating whether this error has null or whitespace <see cref="Code"/> or <see cref="Message"/>.
        /// </summary>
        public bool IsNullOrEmpty =>
            string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Message);

        /// <summary>
        /// Gets a value indicating whether this error contains a non-empty code and message.
        /// </summary>
        public bool HasValue =>
            !IsNullOrEmpty;

        /// <summary>
        /// Gets a default, empty instance of <see cref="ServiceError"/>.
        /// </summary>
        public static IServiceError Default =>
            new ServiceError();

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceError"/> class.
        /// </summary>
        public ServiceError()
        {
            _built = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceError"/> class, configured by the specified action.
        /// </summary>
        /// <param name="configure">An action to configure this error instance.</param>
        public ServiceError(Action<ServiceError> configure)
        {
            configure.Invoke(this);
            _built = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceError"/> class with the specified code and message.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="message">The error message.</param>
        public ServiceError(string code, string message)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            _built = true;
        }

        /// <summary>
        /// Returns a string representation of this error.
        /// </summary>
        /// <returns>
        /// A string in the format "Code: Message", or an empty string if either is null or whitespace.
        /// </returns>
        public override string ToString() =>
            IsNullOrEmpty ? string.Empty : $"{Code}: {Message}";

        /// <summary>
        /// Sets the error code. Only inside the constructor's configure action.
        /// </summary>
        /// <param name="code">The error code to set. If null, sets to an empty string.</param>
        /// <exception cref="InvalidOperationException">The error is already built.</exception>
        public void SetCode(string code)
        {
            EnsureBuilding();
            Code = code ?? string.Empty;
        }

        /// <summary>
        /// Sets the error message. Only inside the constructor's configure action.
        /// </summary>
        /// <param name="message">The error message to set. If null, sets to an empty string.</param>
        /// <exception cref="InvalidOperationException">The error is already built.</exception>
        public void SetMessage(string message)
        {
            EnsureBuilding();
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// Sets both the error code and message.
        /// </summary>
        /// <param name="code">The error code to set. If null, sets to an empty string.</param>
        /// <param name="message">The error message to set. If null, sets to an empty string.</param>
        public void Set(string code, string message)
        {
            SetCode(code);
            SetMessage(message);
        }

        private void EnsureBuilding()
        {
            if (_built)
                throw new InvalidOperationException("A service error is read-only once built.");
        }
    }
}
