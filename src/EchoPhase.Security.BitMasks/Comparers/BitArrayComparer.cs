using System.Collections;

namespace EchoPhase.Security.BitMasks.Comparers
{
    public class BitArrayComparer : IEqualityComparer<BitArray>
    {
        public bool Equals(BitArray? x, BitArray? y)
        {
            if (x == null || y == null || x.Length != y.Length)
                return false;
            for (int i = 0; i < x.Length; i++)
                if (x[i] != y[i])
                    return false;
            return true;
        }

        public int GetHashCode(BitArray obj)
        {
            int hash = 17;
            foreach (bool b in obj)
                hash = hash * 31 + (b ? 1 : 0);
            return hash;
        }
    }
}
