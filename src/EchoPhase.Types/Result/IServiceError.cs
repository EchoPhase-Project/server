// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Types.Result
{
    /// <summary>
    /// Represents an error object used in service result models for conveying error codes and messages.
    /// Read-only: a result's error is shared by every step it is forwarded through.
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
        /// The input the error is about (its JSON name, camelCase), so a form
        /// can show it next to the field; null when it is about the request.
        /// </summary>
        string? Field
        {
            get;
        }

        /// <summary>
        /// Further values agreed for this code (a limit, the allowed types, an
        /// id): what a client may act on beyond the message. Empty by default.
        /// </summary>
        IReadOnlyDictionary<string, object?> Details
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

    }
}
