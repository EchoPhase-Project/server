// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;

namespace EchoPhase.Clients.Models
{
    public class ClientResponse<TResponse, TError> : IClientResponse<TResponse, TError>
    {
        public HttpStatusCode StatusCode
        {
            get; init;
        }
        public TResponse? Data { get; set; } = default;
        public TError? Error { get; set; } = default;
    }
}
