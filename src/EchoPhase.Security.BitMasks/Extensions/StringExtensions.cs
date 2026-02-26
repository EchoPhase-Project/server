using System.Text;

namespace EchoPhase.Security.BitMasks.Extensions
{
    public static class StringExtensions
    {
        public static byte[] ComputeXxHash3(this string input)
        {
            return Encoding.UTF8.GetBytes(input).ComputeXxHash3();
        }
    }
}
