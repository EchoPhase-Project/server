// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Twitch.Dto;
using EchoPhase.Clients.Twitch.Models;

namespace EchoPhase.Clients.Twitch.Auth
{
    public class TwitchAuthClient : ClientBase, ITwitchAuthClient
    {
        private const string AuthorizeEndpoint = "https://id.twitch.tv/oauth2/authorize";

        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        private readonly HttpClient _client;

        public TwitchAuthClient(HttpClient client) : base(client)
        {
            _client = client;
        }

        private static ApiError ToApiError(TwitchApiError? err) =>
            new(err?.Status ?? 0, err?.Message);

        private static Task<TwitchApiError?> ReadErrorAsync(Stream stream, CancellationToken ct) =>
            JsonSerializer.DeserializeAsync<TwitchApiError>(stream, JsonOptions, ct).AsTask();

        public string BuildAuthorizeUrl(
            string clientId,
            string redirectUri,
            IEnumerable<string> scopes,
            string state)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["response_type"] = "code";
            query["client_id"] = clientId;
            query["redirect_uri"] = redirectUri;
            query["scope"] = string.Join(' ', scopes);
            query["state"] = state;
            // Always show the consent screen so reconnecting can switch accounts.
            query["force_verify"] = "true";

            return $"{AuthorizeEndpoint}?{query}";
        }

        public Task<Result<TwitchTokenValidationDto>> ValidateAsync(
            string accessToken,
            CancellationToken ct = default)
            => SendRawAsync<object, object, TwitchTokenValidationDto, TwitchApiError>(
                "validate", HttpMethod.Get, null, null,
                new AuthenticationHeaderValue("OAuth", accessToken),
                readError: ReadErrorAsync,
                toApiError: ToApiError,
                ct);

        public Task<Result<TwitchTokenResponseDto>> ExchangeCodeAsync(
            string clientId,
            string clientSecret,
            string code,
            string redirectUri,
            CancellationToken ct = default)
            => PostTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["redirect_uri"] = redirectUri
            }, ct);

        public Task<Result<TwitchTokenResponseDto>> RefreshAsync(
            string clientId,
            string? clientSecret,
            string refreshToken,
            CancellationToken ct = default)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken
            };

            if (!string.IsNullOrEmpty(clientSecret))
                form["client_secret"] = clientSecret;

            return PostTokenAsync(form, ct);
        }

        // The token endpoint takes a form body; HttpSender only serialises JSON,
        // and secrets must not travel in the query string.
        private async Task<Result<TwitchTokenResponseDto>> PostTokenAsync(
            Dictionary<string, string> form,
            CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "token")
            {
                Content = new FormUrlEncodedContent(form)
            };

            using var response = await _client.SendAsync(request, ct);
            await using var stream = await response.Content.ReadAsStreamAsync(ct);

            return await HttpSender.ReadResultAsync<TwitchTokenResponseDto, TwitchApiError>(
                stream, response.StatusCode, ReadErrorAsync, ToApiError, ct);
        }
    }
}
