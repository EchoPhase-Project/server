// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Clients.Models
{
    public class ClientRequest<TQ, TB> : IClientRequest<TQ, TB>
        where TQ : class
        where TB : class
    {
        public HttpMethod Method { get; set; } = HttpMethod.Get;
        public TQ? Query { get; set; } = default;
        public TB? Body { get; set; } = default;
    }
}
