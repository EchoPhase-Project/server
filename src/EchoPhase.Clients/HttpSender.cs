// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Helpers;
using EchoPhase.Clients.Models;
using Polly;
using Polly.Timeout;

namespace EchoPhase.Clients
{
    public sealed class HttpSender
    {
        private readonly HttpClient _client;
        private readonly QueryStringBuilder _queryStringBuilder;

        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        public HttpSender(HttpClient client)
        {
            _client = client;
            _queryStringBuilder = new QueryStringBuilder();
        }

        // ── Transport ────────────────────────────────────────────────────────

        /// <summary>
        /// Sends an HTTP request and returns the raw status code + response
        /// stream.  The caller is responsible for disposing the stream.
        /// </summary>
        public async Task<(HttpStatusCode StatusCode, Stream Stream)> SendAsync<TQ, TB>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body,
            AuthenticationHeaderValue? auth = null,
            CancellationToken ct = default,
            ConnectionRef? connection = null)
            where TQ : class
            where TB : class
        {
            var baseUri = _client.BaseAddress
                ?? throw new InvalidOperationException("HttpClient.BaseAddress is not set.");

            var url = query is not null
                ? new UriBuilder(new Uri(baseUri, uri))
                { Query = _queryStringBuilder.Build(query) }.ToString()
                : new Uri(baseUri, uri).ToString();

            using var request = new HttpRequestMessage(method, url);

            if (auth is not null)
                request.Headers.Authorization = auth;

            if (connection is not null)
                request.Options.Set(ConnectionRef.OptionKey, connection);

            if (body is not null)
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body, JsonOptions),
                    Encoding.UTF8,
                    MediaTypeNames.Application.Json);

            // ResponseHeadersRead: start streaming immediately; caller reads body.
            var response = await _client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);

            var stream = await response.Content.ReadAsStreamAsync(ct);
            return (response.StatusCode, stream);
        }

        // ── Response readers (static, reusable by every subclass) ────────────

        /// <summary>
        /// Reads a stream into <see cref="IClientResponse{TR,TE}"/>.
        /// Success bodies are deserialised as <typeparamref name="TR"/>;
        /// error bodies as <typeparamref name="TE"/>.
        /// </summary>
        public static async Task<IClientResponse<TR, TE>> ReadClientResponseAsync<TR, TE>(
            Stream stream,
            HttpStatusCode statusCode,
            CancellationToken ct = default)
            where TR : class
            where TE : class
        {
            var response = new ClientResponse<TR, TE> { StatusCode = statusCode };

            if (IsSuccess(statusCode))
                response.Data = await JsonSerializer.DeserializeAsync<TR>(stream, JsonOptions, ct);
            else
                response.Error = await JsonSerializer.DeserializeAsync<TE>(stream, JsonOptions, ct);

            return response;
        }

        /// <summary>
        /// Reads a stream into a <see cref="Result{TR}"/>.
        /// <paramref name="readError"/> deserialises the error body;
        /// <paramref name="toApiError"/> converts it to an <see cref="ApiError"/>.
        /// </summary>
        public static async Task<Result<TR>> ReadResultAsync<TR, TErr>(
            Stream stream,
            HttpStatusCode statusCode,
            Func<Stream, CancellationToken, Task<TErr?>> readError,
            Func<TErr?, ApiError> toApiError,
            CancellationToken ct = default)
            where TR : class
        {
            if (IsSuccess(statusCode))
            {
                TR? data;
                try
                {
                    data = await JsonSerializer.DeserializeAsync<TR>(stream, JsonOptions, ct);
                }
                catch (JsonException)
                {
                    data = null;
                }

                return data is null
                    ? Result<TR>.Fail(Unreadable, HttpStatusCode.BadGateway)
                    : Result<TR>.Ok(data, statusCode);
            }

            TErr? err;
            try
            {
                err = await readError(stream, ct);
            }
            catch (JsonException)
            {
                // Not JSON: a proxy's page in front of the provider.
                return Result<TR>.Fail(Unexplained(statusCode), statusCode);
            }
            return Result<TR>.Fail(toApiError(err), statusCode);
        }

        /// <summary>
        /// Reads a stream into a <see cref="PagedResult{TR}"/> using a wrapper
        /// DTO (<typeparamref name="TWrapper"/>) that contains both data and
        /// pagination cursors.
        /// </summary>
        public static async Task<PagedResult<TR>> ReadPagedResultAsync<TWrapper, TR>(
            Stream stream,
            HttpStatusCode statusCode,
            Func<TWrapper, TR> selectData,
            Func<TWrapper, IPageInfo?> selectPage,
            Func<Stream, CancellationToken, Task<ApiError?>> readError,
            CancellationToken ct = default)
            where TWrapper : class
        {
            if (IsSuccess(statusCode))
            {
                TWrapper? wrapper;
                try
                {
                    wrapper = await JsonSerializer.DeserializeAsync<TWrapper>(stream, JsonOptions, ct);
                }
                catch (JsonException)
                {
                    wrapper = null;
                }

                return wrapper is null
                    ? PagedResult<TR>.Fail(Unreadable, HttpStatusCode.BadGateway)
                    : PagedResult<TR>.Ok(selectData(wrapper), selectPage(wrapper), statusCode);
            }

            ApiError? error;
            try
            {
                error = await readError(stream, ct);
            }
            catch (JsonException)
            {
                error = null;
            }
            return PagedResult<TR>.Fail(error ?? Unexplained(statusCode), statusCode);
        }

        // ── Failures that throw ──────────────────────────────────────────────

        /// <summary>
        /// Runs a call to the provider. What throws instead of answering (the
        /// network, a timeout, the resilience handler refusing) comes back as a
        /// failed result; the caller's own cancellation still throws.
        /// </summary>
        public static async Task<Result<TR>> GuardAsync<TR>(Func<Task<Result<TR>>> call, CancellationToken ct)
        {
            try
            {
                return await call();
            }
            catch (Exception ex) when (Thrown(ex, ct) is { } failure)
            {
                return Result<TR>.Fail(failure.Error, failure.Status);
            }
        }

        /// <inheritdoc cref="GuardAsync{TR}(Func{Task{Result{TR}}}, CancellationToken)"/>
        public static async Task<PagedResult<TR>> GuardAsync<TR>(Func<Task<PagedResult<TR>>> call, CancellationToken ct)
        {
            try
            {
                return await call();
            }
            catch (Exception ex) when (Thrown(ex, ct) is { } failure)
            {
                return PagedResult<TR>.Fail(failure.Error, failure.Status);
            }
        }

        private static (ApiError Error, HttpStatusCode Status)? Thrown(Exception ex, CancellationToken ct) => ex switch
        {
            OperationCanceledException when ct.IsCancellationRequested => null,
            TimeoutRejectedException or OperationCanceledException or TimeoutException
                => (new ApiError((int)HttpStatusCode.GatewayTimeout, "The provider did not answer in time."),
                    HttpStatusCode.GatewayTimeout),
            ExecutionRejectedException
                => (new ApiError((int)HttpStatusCode.ServiceUnavailable,
                        "Requests to the provider are paused after repeated failures."),
                    HttpStatusCode.ServiceUnavailable),
            HttpRequestException
                => (new ApiError((int)HttpStatusCode.BadGateway, "The provider could not be reached."),
                    HttpStatusCode.BadGateway),
            _ => null,
        };

        private static readonly ApiError Unreadable =
            new((int)HttpStatusCode.BadGateway, "The provider's answer could not be read.");

        private static ApiError Unexplained(HttpStatusCode status) =>
            new((int)status, $"The provider answered {(int)status} without a readable error.");

        // ── Helpers ──────────────────────────────────────────────────────────

        private static bool IsSuccess(HttpStatusCode code) =>
            code >= HttpStatusCode.OK && code < HttpStatusCode.MultipleChoices;
    }
}
