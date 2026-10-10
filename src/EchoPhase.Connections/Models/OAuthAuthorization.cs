// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Connections.Models
{
    /// <summary>
    /// Where to send the browser, and the nonce the caller must bind to the
    /// browser (cookie) so the callback can prove it came back to the same user.
    /// </summary>
    public sealed record OAuthAuthorization(string Url, string Nonce);
}
