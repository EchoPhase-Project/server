using System.Reflection;

namespace EchoPhase.Security.BitMasks.Constants
{
    public abstract class ConstantsBase<T> where T : ConstantsBase<T>
    {
        public static IEnumerable<string> AsEnumerable()
        {
            return typeof(T)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .Select(f => f.GetRawConstantValue() as string)
                .Where(s => s != null)
                .Cast<string>();
        }
    }
}
