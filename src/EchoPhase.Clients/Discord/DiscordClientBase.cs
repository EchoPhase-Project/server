// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Http.Headers;
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
        /// Sends a Discord API request authenticated with a Bot token and
        /// returns a <see cref="Result{TR}"/>.
        /// </summary>
        protected Task<Result<TR>> SendAsync<TQ, TB, TR>(
            string uri,
            HttpMethod method,
            TQ? query,
            TB? body,
            string botToken,
            CancellationToken ct = default)
            where TQ : class
            where TB : class
            where TR : class
        {
            var auth = new AuthenticationHeaderValue("Bot", botToken);

            return SendRawAsync<TQ, TB, TR, DiscordApiError>(
                uri, method, query, body, auth,
                readError: (stream, token) =>
                    JsonSerializer.DeserializeAsync<DiscordApiError>(
                        stream, JsonOptions, token).AsTask(),
                toApiError: err =>
                    new ApiError(
                        err?.Code ?? 0,
                        err?.Message,
                        err?.Errors),
                ct);
        }
    }
}
