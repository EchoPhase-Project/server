// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Discord.Models;

namespace EchoPhase.Clients.Discord
{
    public abstract class DiscordClientBase : ClientBase
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        protected DiscordClientBase(HttpClient client) : base(client)
        {
        }

        /// <summary>
        /// Sends a Discord API request on behalf of <paramref name="connection"/>
        /// and returns a <see cref="Result{TR}"/>. The bot token is applied by
        /// the connection authentication handler.
        /// </summary>
        protected Task<Result<TR>> SendAsync<TQ, TB, TR>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body,
            ConnectionRef connection,
            CancellationToken ct = default)
            where TQ : class
            where TB : class
            where TR : class
        {
            return SendRawAsync<TQ, TB, TR, DiscordApiError>(
                uri, method, query, body, null,
                readError: (stream, token) =>
                    JsonSerializer.DeserializeAsync<DiscordApiError>(
                        stream, JsonOptions, token).AsTask(),
                toApiError: err =>
                    new ApiError(
                        err?.Code ?? 0,
                        err?.Message,
                        err?.Errors),
                ct,
                connection);
        }
    }
}
