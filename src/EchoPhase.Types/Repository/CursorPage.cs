// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Projection.Attributes;

namespace EchoPhase.Types.Repository
{
    public class CursorPage<T>
    {
        [Expose]
        public IEnumerable<T> Data { get; init; } = [];
        [Expose]
        public string? NextCursor
        {
            get; init;
        }
        [Expose]
        public bool HasMore => NextCursor is not null;
    }
}
