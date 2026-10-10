// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Clients.Abstractions
{
    /// <summary>
    /// Identifies the stored connection whose credentials a request must be
    /// sent with. Clients only attach it to the request; the authentication
    /// handler in the HttpClient pipeline resolves and applies the credentials.
    /// </summary>
    public sealed record ConnectionRef(Guid UserId, string Name)
    {
        public static readonly HttpRequestOptionsKey<ConnectionRef> OptionKey =
            new("EchoPhase.Connection");
    }
}
