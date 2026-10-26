// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Security.Antiforgery
{
    public interface IAntiforgeryService
    {
        void Set();

        /// <summary>A header token for another tab of the same session, the cookie unchanged.</summary>
        void Reissue();
        void Remove();
        Task ValidateAsync();
    }
}
