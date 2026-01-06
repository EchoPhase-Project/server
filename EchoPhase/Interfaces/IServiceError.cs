using EchoPhase.Services.Results;

namespace EchoPhase.Interfaces
{
    /// <summary>
    /// Represents an error object used in service result models for conveying error codes and messages.
    /// </summary>
    public interface IServiceError
    {
        /// <summary>
        /// Gets the error code representing the type or category of the error.
        /// </summary>
        string Code
        {
            get;
        }

        /// <summary>
        /// Gets the human-readable message that describes the error.
        /// </summary>
        string Message
        {
            get;
        }

        /// <summary>
        /// Gets the string representation of the error, typically combining the code and message.
        /// </summary>
        string Value
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether the error is null or has no meaningful content.
        /// </summary>
        bool IsNullOrEmpty
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether the error has a non-empty code and message.
        /// </summary>
        bool HasValue
        {
            get;
        }

        /// <summary>
        /// Returns the string representation of the error.
        /// </summary>
        /// <returns>A string in the format "Code: Message", or an empty string if the error is empty.</returns>
        string ToString();

        /// <summary>
        /// Applies the given configuration action to the underlying error instance.
        /// </summary>
        /// <param name="configure">An action that modifies the error instance.</param>
        /// <returns>The current <see cref="IServiceError"/> instance for chaining.</returns>
        IServiceError Configure(Action<ServiceError> configure);
    }
}
