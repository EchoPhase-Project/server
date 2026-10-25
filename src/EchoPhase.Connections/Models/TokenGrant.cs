// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics.CodeAnalysis;

namespace EchoPhase.Connections.Models
{
    public enum TokenGrantStatus
    {
        Granted,

        /// <summary>The provider refused the code/refresh token; retrying will not help.</summary>
        Rejected,

        /// <summary>Transient failure; the grant may succeed later.</summary>
        Unavailable
    }

    public sealed record OAuthTokenSet(
        string AccessToken,
        string? RefreshToken,
        IReadOnlyList<string> Scopes,
        int ExpiresIn);

    public sealed record TokenGrant(
        TokenGrantStatus Status,
        OAuthTokenSet? Tokens = null,
        string? Error = null)
    {
        /// <summary>Granted, with its tokens.</summary>
        [MemberNotNullWhen(true, nameof(Tokens))]
        public bool IsGranted => Status == TokenGrantStatus.Granted && Tokens is not null;

        public static TokenGrant Granted(OAuthTokenSet tokens) => new(TokenGrantStatus.Granted, tokens);
        public static TokenGrant Rejected(string? error) => new(TokenGrantStatus.Rejected, null, error);
        public static TokenGrant Unavailable(string? error) => new(TokenGrantStatus.Unavailable, null, error);
    }
}
