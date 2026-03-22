// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Types.Result
{
    /// <summary>
    /// Represents the result of a service operation (no value).
    /// </summary>
    public interface IServiceResult
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        bool Successful { get; }

        /// <summary>
        /// Gets the error associated with the operation, if any.
        /// </summary>
        IServiceError Error { get; }

        /// <summary>
        /// Converts this result to a generic <see cref="IServiceResult{T}"/> containing the specified data.
        /// </summary>
        /// <typeparam name="T">The type of the data to include.</typeparam>
        /// <param name="data">The data to include in the result.</param>
        /// <returns>A new <see cref="IServiceResult{T}"/> instance.</returns>
        IServiceResult<T> To<T>(T data);
    }

    /// <summary>
    /// Represents the result of a service operation that returns a value of type
    /// <typeparamref name="T"/>.
    /// </summary>
    public interface IServiceResult<T> : IServiceResult
    {
        /// <summary>
        /// Gets the value returned by the operation, or default if none.
        /// </summary>
        T? Value { get; }

        // ── Monadic transforms ────────────────────────────────────────────────

        /// <summary>
        /// Projects the value to a new type when the result is successful.
        /// Returns a failed result (same error) when not successful.
        /// </summary>
        IServiceResult<TOut> Map<TOut>(Func<T, TOut> mapper);

        /// <summary>
        /// Chains another operation that itself returns an <see cref="IServiceResult{TOut}"/>.
        /// Short-circuits on failure — the binder is never called.
        /// </summary>
        IServiceResult<TOut> Bind<TOut>(Func<T, IServiceResult<TOut>> binder);

        /// <summary>
        /// Async variant of <see cref="Bind{TOut}"/>.
        /// </summary>
        Task<IServiceResult<TOut>> BindAsync<TOut>(Func<T, Task<IServiceResult<TOut>>> binder);
    }
}
