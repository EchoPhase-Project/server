// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Clients.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ProviderNameAttribute : Attribute
    {
        public string Name { get; }

        public ProviderNameAttribute(string name)
        {
            Name = name;
        }
    }
}
