// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using EchoPhase.Clients.Helpers;
using EchoPhase.Clients.Models;
using EchoPhase.Clients.Abstractions;

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
            CancellationToken ct = default)
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
                var data = await JsonSerializer.DeserializeAsync<TR>(stream, JsonOptions, ct);

                return data is null
                    ? Result<TR>.Fail(
                        new ApiError((int)statusCode, "Response deserialization returned null."),
                        statusCode)
                    : Result<TR>.Ok(data, statusCode);
            }

            var err = await readError(stream, ct);
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
                var wrapper = await JsonSerializer.DeserializeAsync<TWrapper>(stream, JsonOptions, ct);

                return wrapper is null
                    ? PagedResult<TR>.Fail(
                        new ApiError((int)statusCode, "Response deserialization returned null."),
                        statusCode)
                    : PagedResult<TR>.Ok(selectData(wrapper), selectPage(wrapper), statusCode);
            }

            var error = await readError(stream, ct);
            return PagedResult<TR>.Fail(
                error ?? new ApiError((int)statusCode, "Unknown error."),
                statusCode);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static bool IsSuccess(HttpStatusCode code) =>
            code >= HttpStatusCode.OK && code < HttpStatusCode.MultipleChoices;
    }
}
