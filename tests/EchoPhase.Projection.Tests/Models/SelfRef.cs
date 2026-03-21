// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Projection.Attributes;

namespace EchoPhase.Projection.Tests.Models
{
    public class SelfRef
    {
        [Expose] public string Name { get; set; } = "me";
        public SelfRef? Self
        {
            get; set;
        }
    }
}
