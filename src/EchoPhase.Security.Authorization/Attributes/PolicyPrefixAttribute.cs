// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Security.Authorization.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PolicyPrefixAttribute : Attribute
    {
        public string Prefix { get; }

        public PolicyPrefixAttribute(string prefix)
        {
            Prefix = prefix;
        }
    }
}
