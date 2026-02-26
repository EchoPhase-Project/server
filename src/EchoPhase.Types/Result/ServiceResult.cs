namespace EchoPhase.Types.Result
{
    /// <summary>
    /// Represents the result of an operation without a return value, indicating success or failure and optionally providing an error.
    /// </summary>
    public class ServiceResult : IServiceResult
    {
        /// <summary>
        /// Indicates whether the operation was successful.
        /// </summary>
        public bool Successful { get; private set; } = true;

        /// <summary>
        /// The error associated with the operation, if any.
        /// </summary>
        public IServiceError Error { get; private set; } = ServiceError.Default;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceResult"/> class with the specified success status.
        /// </summary>
        /// <param name="success">Whether the operation was successful.</param>
        public ServiceResult(bool success)
        {
            Successful = success;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceResult"/> class with a success flag and an error configured via callback.
        /// </summary>
        /// <param name="success">Whether the operation was successful.</param>
        /// <param name="configure">An action to configure the error object.</param>
        public ServiceResult(bool success, Action<ServiceError> configure) : this(success)
        {
            Error = new ServiceError(configure);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceResult"/> class with a success flag and an existing error object.
        /// </summary>
        /// <param name="success">Whether the operation was successful.</param>
        /// <param name="error">The error to associate with this result.</param>
        public ServiceResult(bool success, IServiceError error) : this(success)
        {
            Error = error;
        }

        /// <summary>
        /// Sets the error and marks the result as failed.
        /// </summary>
        /// <param name="configure">An action to configure the error object.</param>
        /// <returns>The current instance.</returns>
        public IServiceResult SetError(Action<ServiceError> configure)
        {
            Successful = false;
            Error.Configure(configure);
            return this;
        }

        /// <summary>
        /// Converts this non-generic result into a generic result with a specified value.
        /// </summary>
        /// <typeparam name="T">The type of the result value.</typeparam>
        /// <param name="data">The value to include in the result.</param>
        /// <returns>A <see cref="ServiceResult{T}"/> instance with the same success status and error.</returns>
        public IServiceResult<T> To<T>(T data) =>
            new ServiceResult<T>(Successful, Error, data);

        /// <summary>
        /// Creates a successful <see cref="ServiceResult"/>.
        /// </summary>
        public static IServiceResult Success() =>
            new ServiceResult(true);

        /// <summary>
        /// Creates a failed <see cref="ServiceResult"/> with a configured error.
        /// </summary>
        public static IServiceResult Failure(Action<ServiceError> configure) =>
            new ServiceResult(false, configure);

        /// <summary>
        /// Creates a failed <see cref="ServiceResult"/> with the specified error.
        /// </summary>
        public static IServiceResult Failure(IServiceError error) =>
            new ServiceResult(false, error);
    }

    /// <summary>
    /// Represents the result of an operation that returns a value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    public class ServiceResult<T> : ServiceResult, IServiceResult<T>
    {
        /// <summary>
        /// The value returned by the operation, if successful.
        /// </summary>
        public T? Value
        {
            get; private set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceResult{T}"/> class with a success flag and result value.
        /// </summary>
        /// <param name="success">Whether the operation was successful.</param>
        /// <param name="value">The result value.</param>
        public ServiceResult(bool success, T? value = default)
            : base(success)
        {
            Value = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceResult{T}"/> class with a configured error and result value.
        /// </summary>
        /// <param name="success">Whether the operation was successful.</param>
        /// <param name="configure">An action to configure the error object.</param>
        /// <param name="value">The result value.</param>
        public ServiceResult(bool success, Action<ServiceError> configure, T? value = default)
            : base(success, configure)
        {
            Value = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceResult{T}"/> class with a provided error and result value.
        /// </summary>
        /// <param name="success">Whether the operation was successful.</param>
        /// <param name="error">The error to associate with this result.</param>
        /// <param name="value">The result value.</param>
        public ServiceResult(bool success, IServiceError error, T? value = default)
            : base(success, error)
        {
            Value = value;
        }

        /// <summary>
        /// Creates a successful result with the specified value.
        /// </summary>
        /// <param name="value">The result value.</param>
        /// <returns>A successful result containing the value.</returns>
        public static IServiceResult<T> Success(T? value = default) =>
            new ServiceResult<T>(true, value);

        /// <summary>
        /// Creates a failed result with a configured error and optional result value.
        /// </summary>
        /// <param name="configure">An action to configure the error.</param>
        /// <param name="value">The optional result value.</param>
        /// <returns>A failed result containing the error and value.</returns>
        public static IServiceResult<T> Failure(Action<ServiceError> configure, T? value = default) =>
            new ServiceResult<T>(false, configure, value);

        /// <summary>
        /// Creates a failed result with a provided error and optional result value.
        /// </summary>
        /// <param name="error">The error to associate with the result.</param>
        /// <param name="value">The optional result value.</param>
        /// <returns>A failed result containing the error and value.</returns>
        public static IServiceResult<T> Failure(IServiceError error, T? value = default) =>
            new ServiceResult<T>(false, error, value);
    }
}
