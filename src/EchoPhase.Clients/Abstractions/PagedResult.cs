using System.Net;

namespace EchoPhase.Clients.Abstractions
{
    public readonly struct PagedResult<T>
    {
        public T? Value { get; }
        public IPageInfo? Page { get; }
        public ApiError? Error { get; }
        public HttpStatusCode StatusCode { get; }
        public bool IsSuccess => Error is null;

        private PagedResult(T value, IPageInfo? page, HttpStatusCode code)
        {
            Value = value; Page = page; StatusCode = code;
        }

        private PagedResult(ApiError error, HttpStatusCode code)
        {
            Error = error; StatusCode = code;
        }

        public static PagedResult<T> Ok(T value, IPageInfo? page,
            HttpStatusCode code = HttpStatusCode.OK) => new(value, page, code);

        public static PagedResult<T> Fail(ApiError error, HttpStatusCode code) =>
            new(error, code);

        public TOut Match<TOut>(
            Func<T, IPageInfo?, TOut> onSuccess,
            Func<ApiError, TOut> onError) =>
            IsSuccess ? onSuccess(Value!, Page) : onError(Error!);

        public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) =>
            IsSuccess
                ? PagedResult<TOut>.Ok(map(Value!), Page, StatusCode)
                : PagedResult<TOut>.Fail(Error!, StatusCode);
    }
}
