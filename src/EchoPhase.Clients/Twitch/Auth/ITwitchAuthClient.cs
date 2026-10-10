// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Twitch.Dto;

namespace EchoPhase.Clients.Twitch.Auth
{
    /// <summary>
    /// Client for the Twitch identity server (<c>id.twitch.tv/oauth2</c>).
    /// </summary>
    public interface ITwitchAuthClient
    {
        /// <summary>
        /// Builds the URL the user is sent to in order to authorise an application.
        /// </summary>
        string BuildAuthorizeUrl(
            string clientId,
            string redirectUri,
            IEnumerable<string> scopes,
            string state);

        /// <summary>
        /// Validates an access token and returns the application and user it belongs to.
        /// </summary>
        Task<Result<TwitchTokenValidationDto>> ValidateAsync(
            string accessToken,
            CancellationToken ct = default);

        /// <summary>
        /// Exchanges an authorisation code for an access/refresh token pair.
        /// </summary>
        Task<Result<TwitchTokenResponseDto>> ExchangeCodeAsync(
            string clientId,
            string clientSecret,
            string code,
            string redirectUri,
            CancellationToken ct = default);

        /// <summary>
        /// Exchanges a refresh token for a new access/refresh token pair.
        /// <paramref name="clientSecret"/> may be omitted for public clients.
        /// </summary>
        Task<Result<TwitchTokenResponseDto>> RefreshAsync(
            string clientId,
            string? clientSecret,
            string refreshToken,
            CancellationToken ct = default);
    }
}
