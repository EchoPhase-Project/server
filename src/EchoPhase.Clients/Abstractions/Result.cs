using System.Net;

namespace EchoPhase.Clients.Abstractions
{
    public readonly struct Result<T>
    {
        public T? Value { get; }
        public ApiError? Error { get; }
        public HttpStatusCode StatusCode { get; }
        public bool IsSuccess => Error is null;

        private Result(T value, HttpStatusCode code) { Value = value; StatusCode = code; }
        private Result(ApiError error, HttpStatusCode code) { Error = error; StatusCode = code; }

        public static Result<T> Ok(T value, HttpStatusCode code = HttpStatusCode.OK) =>
            new(value, code);

        public static Result<T> Fail(ApiError error, HttpStatusCode code) =>
            new(error, code);

        public TOut Match<TOut>(
            Func<T, TOut> onSuccess,
            Func<ApiError, TOut> onError) =>
            IsSuccess ? onSuccess(Value!) : onError(Error!);

        public void Switch(Action<T> onSuccess, Action<ApiError> onError)
        {
            if (IsSuccess) onSuccess(Value!);
            else onError(Error!);
        }

        public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
            IsSuccess
                ? Result<TOut>.Ok(map(Value!), StatusCode)
                : Result<TOut>.Fail(Error!, StatusCode);
    }
}
