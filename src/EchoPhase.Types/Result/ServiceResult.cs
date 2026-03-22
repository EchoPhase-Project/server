// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Types.Result
{
    // ── Non-generic ───────────────────────────────────────────────────────────

    /// <summary>
    /// Represents the result of a service operation that does not return a value.
    /// Used for operations that either complete successfully or produce an error.
    /// </summary>
    public class ServiceResult : IServiceResult
    {
        /// <summary>
        /// Indicates whether the operation completed successfully.
        /// </summary>
        public bool Successful { get; private set; } = true;

        /// <summary>
        /// Contains error information when the operation did not succeed.
        /// Defaults to <see cref="ServiceError.Default"/> when no error is present.
        /// </summary>
        public IServiceError Error { get; private set; } = ServiceError.Default;

        /// <summary>
        /// Initialises a new instance with the specified success status.
        /// </summary>
        /// <param name="success">Whether the operation succeeded.</param>
        public ServiceResult(bool success) => Successful = success;

        /// <summary>
        /// Initialises a new instance with the specified success status and a configurable error.
        /// </summary>
        /// <param name="success">Whether the operation succeeded.</param>
        /// <param name="configure">A delegate used to configure the <see cref="ServiceError"/> instance.</param>
        public ServiceResult(bool success, Action<ServiceError> configure) : this(success)
            => Error = new ServiceError(configure);

        /// <summary>
        /// Initialises a new instance with the specified success status and an existing error object.
        /// </summary>
        /// <param name="success">Whether the operation succeeded.</param>
        /// <param name="error">An error object implementing <see cref="IServiceError"/>.</param>
        public ServiceResult(bool success, IServiceError error) : this(success)
            => Error = error;

        /// <summary>
        /// Promotes this non-generic result to a generic <see cref="IServiceResult{T}"/>
        /// by attaching a value, while preserving the current status and error.
        /// </summary>
        /// <typeparam name="T">The type of the value to attach.</typeparam>
        /// <param name="data">The value to include in the promoted result.</param>
        /// <returns>A generic result with the same status and error as this instance.</returns>
        public IServiceResult<T> To<T>(T data) =>
            new ServiceResult<T>(Successful, Error, data);

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <returns>A result with a success status.</returns>
        public static IServiceResult Success() => new ServiceResult(true);

        /// <summary>
        /// Creates a failed result with a configurable error.
        /// </summary>
        /// <param name="configure">A delegate used to configure the <see cref="ServiceError"/> instance.</param>
        /// <returns>A result with a failure status.</returns>
        public static IServiceResult Failure(Action<ServiceError> configure) =>
            new ServiceResult(false, configure);

        /// <summary>
        /// Creates a failed result with an existing error object.
        /// </summary>
        /// <param name="error">An error object implementing <see cref="IServiceError"/>.</param>
        /// <returns>A result with a failure status.</returns>
        public static IServiceResult Failure(IServiceError error) =>
            new ServiceResult(false, error);
    }

    // ── Generic ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Represents the result of a service operation that returns a value of type <typeparamref name="T"/>.
    /// Supports functional <c>Map</c> and <c>Bind</c> operations for chaining steps
    /// in a Railway-Oriented Programming style — once a failure occurs, subsequent
    /// steps are skipped and the error propagates automatically.
    /// </summary>
    /// <typeparam name="T">The type of the value produced by the operation.</typeparam>
    public class ServiceResult<T> : ServiceResult, IServiceResult<T>
    {
        /// <summary>
        /// The value produced by the operation.
        /// May be <c>null</c> on failure or when the operation yields no value.
        /// </summary>
        public T? Value { get; private set; }

        /// <summary>
        /// Initialises a new instance with the specified success status and value.
        /// </summary>
        /// <param name="success">Whether the operation succeeded.</param>
        /// <param name="value">The value produced by the operation.</param>
        public ServiceResult(bool success, T? value = default) : base(success)
            => Value = value;

        /// <summary>
        /// Initialises a new instance with the specified success status, configurable error, and value.
        /// </summary>
        /// <param name="success">Whether the operation succeeded.</param>
        /// <param name="configure">A delegate used to configure the <see cref="ServiceError"/> instance.</param>
        /// <param name="value">The value produced by the operation.</param>
        public ServiceResult(bool success, Action<ServiceError> configure, T? value = default)
            : base(success, configure)
            => Value = value;

        /// <summary>
        /// Initialises a new instance with the specified success status, existing error object, and value.
        /// </summary>
        /// <param name="success">Whether the operation succeeded.</param>
        /// <param name="error">An error object implementing <see cref="IServiceError"/>.</param>
        /// <param name="value">The value produced by the operation.</param>
        public ServiceResult(bool success, IServiceError error, T? value = default)
            : base(success, error)
            => Value = value;

        // ── Map ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Transforms the value inside a successful result using <paramref name="mapper"/>,
        /// wrapping the output in a new <see cref="IServiceResult{TOut}"/>.
        /// If the current result is a failure or <see cref="Value"/> is <c>null</c>,
        /// <paramref name="mapper"/> is not invoked and the existing error is forwarded.
        /// Any exception thrown by <paramref name="mapper"/> is caught and converted into a failure result.
        /// </summary>
        /// <typeparam name="TOut">The type of the transformed value.</typeparam>
        /// <param name="mapper">A function that transforms a value of <typeparamref name="T"/> into <typeparamref name="TOut"/>.</param>
        /// <returns>
        /// A successful result containing the transformed value, or a failure result
        /// if this instance already failed or <paramref name="mapper"/> threw an exception.
        /// </returns>
        public IServiceResult<TOut> Map<TOut>(Func<T, TOut> mapper)
        {
            if (!Successful || Value is null)
                return ServiceResult<TOut>.Failure(Error);
            try
            {
                return ServiceResult<TOut>.Success(mapper(Value));
            }
            catch (Exception ex)
            {
                return ServiceResult<TOut>.Failure(err =>
                    err.Set(ex.GetType().Name, ex.Message));
            }
        }

        // ── Bind ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Passes the value of a successful result into <paramref name="binder"/>,
        /// which itself returns an <see cref="IServiceResult{TOut}"/>.
        /// Use this to chain operations where each step can independently succeed or fail.
        /// If the current result is a failure or <see cref="Value"/> is <c>null</c>,
        /// <paramref name="binder"/> is not invoked and the existing error is forwarded.
        /// Any exception thrown by <paramref name="binder"/> is caught and converted into a failure result.
        /// </summary>
        /// <typeparam name="TOut">The type of the value returned by the next operation.</typeparam>
        /// <param name="binder">A function that receives the current value and returns a new result.</param>
        /// <returns>
        /// The result produced by <paramref name="binder"/>, or a failure result
        /// if this instance already failed or <paramref name="binder"/> threw an exception.
        /// </returns>
        public IServiceResult<TOut> Bind<TOut>(Func<T, IServiceResult<TOut>> binder)
        {
            if (!Successful || Value is null)
                return ServiceResult<TOut>.Failure(Error);
            try
            {
                return binder(Value);
            }
            catch (Exception ex)
            {
                return ServiceResult<TOut>.Failure(err =>
                    err.Set(ex.GetType().Name, ex.Message));
            }
        }

        // ── BindAsync ─────────────────────────────────────────────────────────

        /// <summary>
        /// Asynchronous variant of <see cref="Bind{TOut}"/>.
        /// Passes the value of a successful result into the async <paramref name="binder"/>,
        /// which returns a <see cref="Task{IServiceResult{TOut}}"/>.
        /// If the current result is a failure or <see cref="Value"/> is <c>null</c>,
        /// <paramref name="binder"/> is not invoked and the existing error is forwarded.
        /// Any exception thrown by <paramref name="binder"/> is caught and converted into a failure result.
        /// </summary>
        /// <typeparam name="TOut">The type of the value returned by the next async operation.</typeparam>
        /// <param name="binder">An async function that receives the current value and returns a new result.</param>
        /// <returns>
        /// A task whose result is the value produced by <paramref name="binder"/>, or a failure result
        /// if this instance already failed or <paramref name="binder"/> threw an exception.
        /// </returns>
        public async Task<IServiceResult<TOut>> BindAsync<TOut>(
            Func<T, Task<IServiceResult<TOut>>> binder)
        {
            if (!Successful || Value is null)
                return ServiceResult<TOut>.Failure(Error);
            try
            {
                return await binder(Value);
            }
            catch (Exception ex)
            {
                return ServiceResult<TOut>.Failure(err =>
                    err.Set(ex.GetType().Name, ex.Message));
            }
        }

        // ── Static factory ────────────────────────────────────────────────────

        /// <summary>
        /// Creates a successful result with the specified value.
        /// </summary>
        /// <param name="value">The value produced by the operation.</param>
        /// <returns>A result with a success status and the provided value.</returns>
        public static IServiceResult<T> Success(T? value = default) =>
            new ServiceResult<T>(true, value);

        /// <summary>
        /// Creates a failed result with a configurable error and an optional value.
        /// </summary>
        /// <param name="configure">A delegate used to configure the <see cref="ServiceError"/> instance.</param>
        /// <param name="value">An optional value that may accompany the error.</param>
        /// <returns>A result with a failure status.</returns>
        public static IServiceResult<T> Failure(Action<ServiceError> configure, T? value = default) =>
            new ServiceResult<T>(false, configure, value);

        /// <summary>
        /// Creates a failed result with an existing error object and an optional value.
        /// </summary>
        /// <param name="error">An error object implementing <see cref="IServiceError"/>.</param>
        /// <param name="value">An optional value that may accompany the error.</param>
        /// <returns>A result with a failure status.</returns>
        public static IServiceResult<T> Failure(IServiceError error, T? value = default) =>
            new ServiceResult<T>(false, error, value);
    }
}
