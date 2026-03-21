// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Projection.Attributes;

namespace EchoPhase.Projection.Tests.Models
{
    public class NestedModel
    {
        [Expose] public Guid Id { get; set; } = Guid.NewGuid();
        [Expose] public AddressModel Address { get; set; } = new();
        public string Hidden { get; set; } = "hidden";
    }
}
