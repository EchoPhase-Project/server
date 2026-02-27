using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Extensions
{
    public static class EnumExtensions
    {
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
