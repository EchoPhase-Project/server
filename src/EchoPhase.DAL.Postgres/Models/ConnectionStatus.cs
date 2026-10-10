// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Postgres.Models
{
    public enum ConnectionStatus
    {
        Active = 0,

        /// <summary>
        /// The provider no longer accepts the stored tokens; they have been
        /// cleared and the user must reconnect (or supply a new token).
        /// </summary>
        ReauthRequired = 1
    }
}
