// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Discord.Models;

namespace EchoPhase.Clients.Discord
{
    public abstract class DiscordClientBase
    {
        private readonly HttpSender _sender;

        private static readonly JsonSerializerOptions _jsonOptions =
            new(JsonSerializerDefaults.Web);

        protected DiscordClientBase(HttpClient client)
        {
            _sender = new HttpSender(client);
        }

        protected async Task<Result<TR>> SendAsync<TQ, TB, TR>(
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
            var (statusCode, stream) = await _sender.SendAsync(
                uri, method, query, body,
                new AuthenticationHeaderValue("Bot", botToken),
                ct);

            await using (stream)
            {
                if (statusCode >= HttpStatusCode.OK && statusCode < HttpStatusCode.MultipleChoices)
                {
                    var data = await JsonSerializer.DeserializeAsync<TR>(stream, _jsonOptions, ct);

                    if (data is null)
                        return Result<TR>.Fail(
                            new ApiError((int)statusCode, "Response deserialization returned null."),
                            statusCode);

                    return Result<TR>.Ok(data, statusCode);
                }

                var error = await JsonSerializer.DeserializeAsync<DiscordApiError>(stream, _jsonOptions, ct);
                return Result<TR>.Fail(
                    new ApiError(error?.Code ?? (int)statusCode, error?.Message, error?.Errors),
                    statusCode);
            }
        }
    }
}
