namespace EchoPhase.Scripting.Extensions
{
    public static class ObjectExtensions
    {
        public static T Also<T>(this T obj, Action<T> action)
        {
            action(obj);
            return obj;
        }
    }
}
