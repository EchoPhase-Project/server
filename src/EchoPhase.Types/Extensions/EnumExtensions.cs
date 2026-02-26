namespace EchoPhase.Types.Extensions
{
    public static class EnumExtensions
    {
        public static bool TryParseEnum<T>(this string value, out T result) where T : struct
        {
            result = default(T);
            if (!typeof(T).IsEnum)
                return false;

            return Enum.TryParse(value, true, out result);
        }
    }
}
