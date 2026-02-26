using System.Collections;

namespace EchoPhase.Types.Extensions
{
    public static class StringExtensions
    {
        public static BitArray ToBitArray(this string base64) =>
            new BitArray(Convert.FromBase64String(base64));
    }
}
