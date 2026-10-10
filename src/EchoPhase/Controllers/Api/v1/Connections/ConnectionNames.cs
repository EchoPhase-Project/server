// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Controllers.Api.v1.Connections
{
    internal static class ConnectionNames
    {
        public const string Pattern = "^[A-Za-z0-9_.-]{1,64}$";
        public const string Message = "Use 1-64 letters, digits, '_', '.' or '-'.";
    }
}
