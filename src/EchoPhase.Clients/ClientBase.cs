// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Http.Headers;
using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Models;

namespace EchoPhase.Clients
{
    public abstract class ClientBase
    {
        private readonly HttpSender _sender;

        protected ClientBase(HttpClient client)
        {
            _sender = new HttpSender(client);
        }

        // ── protected helpers for subclasses ─────────────────────────────────

        protected static AuthenticationHeaderValue WithAuth(string scheme, string token) =>
            new(scheme, token);

        // ── typed response (IClientResponse<TR, TE>) ─────────────────────────

        /// <summary>
        /// Sends a request and deserialises both success and error bodies into
        /// strongly-typed wrappers.
        /// </summary>
        protected async Task<IClientResponse<TR, TE>> SendAsync<TQ, TB, TR, TE>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body,
            AuthenticationHeaderValue? auth = null,
            CancellationToken ct = default)
            where TQ : class
            where TB : class
            where TR : class
            where TE : class
        {
            var (statusCode, stream) = await _sender
                .SendAsync(uri, method, query, body, auth, ct);

            await using (stream)
            {
                return await HttpSender
                    .ReadClientResponseAsync<TR, TE>(stream, statusCode, ct);
            }
        }

        // ── discriminated Result<TR> (used by Discord / Twitch bases) ────────

        /// <summary>
        /// Sends a request and returns a <see cref="Result{TR}"/> whose error
        /// side is produced by <paramref name="readError"/>.
        /// </summary>
        protected async Task<Result<TR>> SendRawAsync<TQ, TB, TR, TErr>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body,
            AuthenticationHeaderValue? auth,
            Func<System.IO.Stream, CancellationToken, Task<TErr?>> readError,
            Func<TErr?, ApiError> toApiError,
            CancellationToken ct = default)
            where TQ : class
            where TB : class
            where TR : class
        {
            var (statusCode, stream) = await _sender
                .SendAsync(uri, method, query, body, auth, ct);

            await using (stream)
            {
                return await HttpSender
                    .ReadResultAsync<TR, TErr>(stream, statusCode, readError, toApiError, ct);
            }
        }

        // ── paged result ─────────────────────────────────────────────────────

        /// <summary>
        /// Sends a request and returns a <see cref="PagedResult{TR}"/> using
        /// a wrapper DTO that carries both <c>data</c> and pagination info.
        /// </summary>
        protected async Task<PagedResult<TR>> SendPagedAsync<TQ, TB, TWrapper, TR>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body,
            AuthenticationHeaderValue? auth,
            Func<TWrapper, TR> selectData,
            Func<TWrapper, IPageInfo?> selectPage,
            Func<System.IO.Stream, CancellationToken, Task<ApiError?>> readError,
            CancellationToken ct = default)
            where TQ : class
            where TB : class
            where TWrapper : class
        {
            var (statusCode, stream) = await _sender
                .SendAsync(uri, method, query, body, auth, ct);

            await using (stream)
            {
                return await HttpSender
                    .ReadPagedResultAsync<TWrapper, TR>(
                        stream, statusCode, selectData, selectPage, readError, ct);
            }
        }
    }
}
