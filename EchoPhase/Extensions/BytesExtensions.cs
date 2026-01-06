using System.Collections;
using System.IO.Hashing;

namespace EchoPhase.Extensions
{
    public static class BytesExtensions
    {
        public static byte[] ComputeXxHash3(this ReadOnlySpan<byte> data)
        {
            Span<byte> hash = stackalloc byte[8];
            XxHash3.Hash(data, hash);
            return hash.ToArray();
        }

        public static byte[] ComputeXxHash3(this byte[] data)
        {
            return ((ReadOnlySpan<byte>)data).ComputeXxHash3();
        }

        public static byte[] ComputeXxHash3(this Span<byte> data)
        {
            return ((ReadOnlySpan<byte>)data).ComputeXxHash3();
        }

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
