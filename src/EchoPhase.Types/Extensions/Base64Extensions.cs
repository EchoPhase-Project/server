namespace EchoPhase.Types.Extensions
{
    public static class Base64Extensions
    {
        public static bool TryFromBase64String(this string base64, out byte[] result)
        {
            result = Array.Empty<byte>();

            if (string.IsNullOrWhiteSpace(base64))
                return false;

            int padding = CountBase64Padding(base64);

            int estimatedSize = (base64.Length * 3 / 4) - padding;
            Span<byte> buffer = stackalloc byte[estimatedSize];

            if (Convert.TryFromBase64String(base64, buffer, out int written))
            {
                result = buffer.Slice(0, written).ToArray();
                return true;
            }

            return false;
        }

        private static int CountBase64Padding(string base64)
        {
            int padding = 0;

            for (int i = base64.Length - 1; i >= 0; i--)
            {
                if (base64[i] == '=') padding++;
                else break;
            }

            return padding;
        }
    }
}
