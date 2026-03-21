// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Clients.Models
{
    public interface IClientRequest<out TQ, out TB>
        where TQ : class
        where TB : class
    {
        HttpMethod Method
        {
            get;
        }
        TQ? Query
        {
            get;
        }
        TB? Body
        {
            get;
        }
    }
}
