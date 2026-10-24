// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Types.Attributes
{
    /// <summary>
    /// An enum whose set of values grows over time (block types, say): the
    /// API contract declares it extensible (x-extensible-enum, not enum), so
    /// a new value is not a breaking change and clients are told to expect
    /// values they do not know yet.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum)]
    public sealed class ExtensibleEnumAttribute : Attribute
    {
    }
}
