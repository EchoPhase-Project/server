// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;

namespace EchoPhase.DAL.Scylla
{
    /// <summary>Reflection that says what it could not find, instead of returning null.</summary>
    internal static class Reflect
    {
        private const BindingFlags Everything =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public static MethodInfo Method(Type type, string name, params Type[] parameters)
            => (parameters.Length == 0 ? type.GetMethod(name, Everything) : type.GetMethod(name, Everything, parameters))
               ?? throw new MissingMethodException(type.FullName, name);

        public static PropertyInfo Property(Type type, string name)
            => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
               ?? throw new MissingMemberException(type.FullName, name);

        public static T Create<T>(Type type, params object?[] arguments)
            => Activator.CreateInstance(type, arguments) is T made
                ? made
                : throw new InvalidOperationException($"Cannot create a {type.Name} as {typeof(T).Name}.");

        public static object? Invoke(MethodInfo method, object? target, params object?[] arguments)
            => method.Invoke(target, arguments);
    }
}
