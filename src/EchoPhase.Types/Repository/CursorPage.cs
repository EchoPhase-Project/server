// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Projection.Attributes;

namespace EchoPhase.Types.Repository
{
    public class CursorPage<T, TCursor>
        where TCursor : notnull
    {
        [Expose]
        public IEnumerable<T> Data { get; init; } = [];

        [Expose]
        public TCursor? NextCursor { get; init; } = default;

        [Expose]
        public bool HasMore => NextCursor is not null;
    }

    public class CursorPage<T> : CursorPage<T, string> { }
}
