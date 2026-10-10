// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Controllers.Api.v1.Common
{
    /// <summary>One page of a cursor-paginated list; pass NextCursor to get the next one.</summary>
    public sealed record CursorPageResponse<T>(IReadOnlyList<T> Data, string? NextCursor)
    {
        public bool HasMore => NextCursor is not null;
    }
}
