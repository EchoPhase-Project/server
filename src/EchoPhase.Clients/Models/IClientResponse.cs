// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;

namespace EchoPhase.Clients.Models
{
    public interface IClientResponse<out TResponse, out TError>
    {
        HttpStatusCode StatusCode
        {
            get;
        }
        TError? Error
        {
            get;
        }
        TResponse? Data
        {
            get;
        }
    }
}
