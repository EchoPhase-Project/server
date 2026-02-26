using System.IO.Hashing;

namespace EchoPhase.Security.BitMasks.Extensions
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
    }
}
