// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.


namespace EchoPhase.DAL.Abstractions.Repository
{
    public class CursorPage<T, TCursor>
        where TCursor : notnull
    {
        public IEnumerable<T> Data { get; init; } = [];

        public TCursor? NextCursor { get; init; } = default;

        public bool HasMore => NextCursor is not null;
    }

    public class CursorPage<T> : CursorPage<T, string> { }
}
