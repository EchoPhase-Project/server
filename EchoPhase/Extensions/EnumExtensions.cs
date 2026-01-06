using EchoPhase.Attributes;
using EchoPhase.Processors.Enums;

namespace EchoPhase.Extensions
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

        public static bool IsIgnored(this OpCodes opCode)
        {
            var memberInfo = typeof(OpCodes).GetMember(opCode.ToString());
            if (memberInfo.Length > 0)
            {
                return memberInfo[0].GetCustomAttributes(typeof(IgnoreOpCodeAttribute), false).Any();
            }
            return false;
        }
    }
}
