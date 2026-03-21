// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tests.Models
{
    public class Person
    {
        public string Name { get; set; } = "";
        public int Age
        {
            get; set;
        }
        public Address Address { get; set; } = new();
    }
}
