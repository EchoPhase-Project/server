// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Connections.Models
{
    public enum TokenValidationStatus
    {
        Valid,

        /// <summary>The provider rejected the token.</summary>
        Invalid,

        /// <summary>The provider could not answer; the token's state is unknown.</summary>
        Unavailable
    }

    public sealed record TokenIdentity(
        string? ClientId,
        string? ExternalAccountId,
        string? Login,
        IReadOnlyList<string> Scopes,
        int? ExpiresIn);

    public sealed record TokenValidation(
        TokenValidationStatus Status,
        TokenIdentity? Identity = null,
        string? Error = null)
    {
        public static TokenValidation Valid(TokenIdentity? identity) => new(TokenValidationStatus.Valid, identity);
        public static TokenValidation Invalid(string? error) => new(TokenValidationStatus.Invalid, null, error);
        public static TokenValidation Unavailable(string? error) => new(TokenValidationStatus.Unavailable, null, error);
    }
}
