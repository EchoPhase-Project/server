// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace EchoPhase.Clients.Abstractions
{
    public readonly struct Result<T>
    {
        public T? Value { get; }
        public ApiError? Error { get; }
        public HttpStatusCode StatusCode { get; }
        /// <summary>Succeeded: Value is set; otherwise Error is.</summary>
        [MemberNotNullWhen(true, nameof(Value))]
        [MemberNotNullWhen(false, nameof(Error))]
        public bool IsSuccess { get; }

        private Result(T value, HttpStatusCode code) { Value = value; StatusCode = code; IsSuccess = true; }
        private Result(ApiError error, HttpStatusCode code) { Error = error; StatusCode = code; }

        public static Result<T> Ok(T value, HttpStatusCode code = HttpStatusCode.OK) =>
            new(value, code);

        public static Result<T> Fail(ApiError error, HttpStatusCode code) =>
            new(error, code);

        public TOut Match<TOut>(
            Func<T, TOut> onSuccess,
            Func<ApiError, TOut> onError) =>
            IsSuccess ? onSuccess(Value) : onError(Error);

        public void Switch(Action<T> onSuccess, Action<ApiError> onError)
        {
            if (IsSuccess) onSuccess(Value);
            else onError(Error);
        }

        public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
            IsSuccess
                ? Result<TOut>.Ok(map(Value), StatusCode)
                : Result<TOut>.Fail(Error, StatusCode);
    }
}
