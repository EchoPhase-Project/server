// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

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
