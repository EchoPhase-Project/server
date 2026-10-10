// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Http.Headers;
using EchoPhase.Connections.Models;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Connections.Providers
{
    public class DiscordConnectionProvider : IConnectionProvider
    {
        public string Name => ConnectionProviders.Discord;

        public ConnectionAuthType ManualAuthType => ConnectionAuthType.BotToken;

        public void Apply(HttpRequestMessage request, ResolvedCredential credential)
            => request.Headers.Authorization = new AuthenticationHeaderValue("Bot", credential.AccessToken);

        // Bot tokens are not checked up front; a bad one surfaces as a 401 on
        // first use and marks the connection for re-authentication.
        public Task<TokenValidation> ValidateAsync(string accessToken, CancellationToken ct = default)
            => Task.FromResult(TokenValidation.Valid(null));
    }
}
