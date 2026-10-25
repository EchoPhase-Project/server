// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace EchoPhase.Clients.Abstractions
{
    public readonly struct PagedResult<T>
    {
        public T? Value { get; }
        public IPageInfo? Page { get; }
        public ApiError? Error { get; }
        public HttpStatusCode StatusCode { get; }
        /// <summary>Succeeded: Value is set; otherwise Error is.</summary>
        [MemberNotNullWhen(true, nameof(Value))]
        [MemberNotNullWhen(false, nameof(Error))]
        public bool IsSuccess { get; }

        private PagedResult(T value, IPageInfo? page, HttpStatusCode code)
        {
            Value = value; Page = page; StatusCode = code; IsSuccess = true;
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
            IsSuccess ? onSuccess(Value, Page) : onError(Error);

        public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) =>
            IsSuccess
                ? PagedResult<TOut>.Ok(map(Value), Page, StatusCode)
                : PagedResult<TOut>.Fail(Error, StatusCode);
    }
}
