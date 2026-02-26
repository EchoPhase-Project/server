using System.Collections;

namespace EchoPhase.Types.Extensions
{
    public static class BytesExtensions
    {
        public static byte ReverseBits(this byte b)
        {
            return (byte)((b * 0x0202020202UL & 0x010884422010UL) % 1023);
        }

        public static void ReverseBytes(this byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = bytes[i].ReverseBits();
        }

        public static BitArray ToBitArray(this byte[] bytes, int? bitLength = null)
        {
            if (bitLength < 0)
                throw new ArgumentOutOfRangeException(nameof(bitLength));

            var bitArray = new BitArray(bytes);
            if (bitLength.HasValue && bitLength.Value < bitArray.Length)
            {
                bitArray.Length = bitLength.Value;
            }
            return bitArray;
        }
    }
}
