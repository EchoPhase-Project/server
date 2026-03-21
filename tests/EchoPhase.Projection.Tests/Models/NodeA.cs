// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Projection.Attributes;

namespace EchoPhase.Projection.Tests.Models
{
    public class NodeA
    {
        [Expose] public string Label { get; set; } = "A";
        [Expose]
        public NodeB? Child
        {
            get; set;
        }
    }
}
