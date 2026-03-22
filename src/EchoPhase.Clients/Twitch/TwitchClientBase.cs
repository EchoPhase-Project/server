// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Clients.Twitch
{
    using System.Net.Http.Headers;
    using System.Text.Json;
    using EchoPhase.Clients.Abstractions;
    using EchoPhase.Clients.Twitch.Models;

    public abstract class TwitchClientBase : ClientBase
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        protected TwitchClientBase(HttpClient client) : base(client)
        {
        }

        /// <summary>
        /// Sends a Twitch API request authenticated with a Bearer token and
        /// returns a paged result.  The Twitch response envelope wraps data
        /// inside a <c>data</c> array plus a <c>pagination</c> cursor object.
        /// </summary>
        protected Task<PagedResult<IEnumerable<TR>>> SendAsync<TQ, TB, TR>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body,
            string bearerToken,
            CancellationToken ct = default)
            where TQ : class
            where TB : class
            where TR : class
        {
            var auth = new AuthenticationHeaderValue("Bearer", bearerToken);

            return SendPagedAsync<TQ, TB, TwitchApiResponseDto<TR>, IEnumerable<TR>>(
                uri, method, query, body, auth,
                selectData: wrapper => wrapper.Data ?? Enumerable.Empty<TR>(),
                selectPage: wrapper => wrapper.Pagination is null
                    ? null
                    : new PageInfo(wrapper.Pagination.Cursor),
                readError: async (stream, token) =>
                {
                    var err = await JsonSerializer.DeserializeAsync<TwitchApiError>(
                        stream, JsonOptions, token);

                    return err is null
                        ? null
                        : new ApiError(0, err.Message);
                },
                ct);
        }

        /// <summary>
        /// Sends a Twitch API request and returns a plain <see cref="Result{TR}"/>
        /// (non-paged variant).
        /// </summary>
        protected Task<Result<TR>> SendRawAsync<TQ, TB, TR>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body,
            string bearerToken,
            CancellationToken ct = default)
            where TQ : class
            where TB : class
            where TR : class
        {
            var auth = new AuthenticationHeaderValue("Bearer", bearerToken);

            return SendRawAsync<TQ, TB, TR, TwitchApiError>(
                uri, method, query, body, auth,
                readError: (stream, token) =>
                    JsonSerializer.DeserializeAsync<TwitchApiError>(
                        stream, JsonOptions, token).AsTask(),
                toApiError: err =>
                    new ApiError(0, err?.Message),
                ct);
        }
    }
}
