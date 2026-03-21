// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.QRCodes.Generators
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class QRFormatAttribute : Attribute
    {
        public string Format
        {
            get;
        }

        public QRFormatAttribute(string format)
        {
            Format = format.ToLowerInvariant();
        }
    }

}
