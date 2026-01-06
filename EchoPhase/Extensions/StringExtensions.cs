using System.Collections;
using System.Text;

namespace EchoPhase.Extensions
{
    public static class StringExtensions
    {
        public static byte[] ComputeXxHash3(this string input)
        {
            return Encoding.UTF8.GetBytes(input).ComputeXxHash3();
        }

        public static BitArray ToBitArray(this string base64) =>
            new BitArray(Convert.FromBase64String(base64));
    }
}
