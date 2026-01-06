using System.Collections;
using System.Text;

namespace EchoPhase.Extensions
{
    public static class BitArrayExtensions
    {
        public static byte[] ToByteArray(this BitArray bits)
        {
            int numBytes = (bits.Length + 7) / 8;
            byte[] bytes = new byte[numBytes];
            bits.CopyTo(bytes, 0);
            return bytes;
        }

        public static string ToBinaryString(this BitArray bits)
        {
            var sb = new StringBuilder(bits.Length);
            for (int i = 0; i < bits.Length; i++)
            {
                sb.Append(bits[i] ? '1' : '0');
            }
            return sb.ToString();
        }

        public static string ToHexString(this BitArray bits)
        {
            int numBytes = (bits.Length + 7) / 8;
            byte[] bytes = new byte[numBytes];
            bits.CopyTo(bytes, 0);
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        public static string ToBase64String(this BitArray bits) =>
            Convert.ToBase64String(bits.ToByteArray());

        public static bool AllRequiredBitsSet(this BitArray check, BitArray required)
        {
            if (check.Length < required.Length)
                return false;

            for (int i = 0; i < required.Length; i++)
                if (required[i] && !check[i])
                    return false;

            return true;
        }

        public static bool AnyRequiredBitsSet(this BitArray check, BitArray required)
        {
            if (check.Length < required.Length)
                return false;

            for (int i = 0; i < required.Length; i++)
            {
                if (required[i] && check[i])
                    return true;
            }

            return false;
        }
    }
}
