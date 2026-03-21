// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;

namespace EchoPhase.Security.BitMasks.Comparers
{
    public class BitArrayComparer : IEqualityComparer<BitArray>
    {
        public static readonly BitArrayComparer Instance = new();

        public bool Equals(BitArray? x, BitArray? y)
        {
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++)
                if (x[i] != y[i]) return false;
            return true;
        }

        public int GetHashCode(BitArray obj)
        {
            var hash = new HashCode();
            hash.Add(obj.Length);
            foreach (bool bit in obj)
                hash.Add(bit);
            return hash.ToHashCode();
        }
    }
}
