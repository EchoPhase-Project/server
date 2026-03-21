// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Projection.Tests.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Projection.Tests
{
    public class CyclicReferenceTests : TestBase, IClassFixture<ConfiguredFixture>
    {
        private readonly Projector _projector;

        public CyclicReferenceTests(ConfiguredFixture fixture) =>
            _projector = fixture.Provider.GetRequiredService<Projector>();

        [Fact]
        public void IndirectCycle_DoesNotThrow()
        {
            var a = new NodeA();
            var b = new NodeB { Parent = a };
            a.Child = b;

            Assert.Null(Record.Exception(() => _projector.For(a).Build()));
        }

        [Fact]
        public void DirectSelfReference_DoesNotThrow()
        {
            var node = new SelfRef();
            node.Self = node;

            Assert.Null(Record.Exception(() => _projector.For(node).Build()));
        }

        [Fact]
        public void SelfReference_CycleBreaksWithNull()
        {
            var node = new SelfRef();
            node.Self = node;

            var result = AsDictionary(_projector.For(node).Build());
            Assert.Equal("me", result["Name"]);

            if (result.ContainsKey("Self") && result["Self"] is Dictionary<string, object?> selfDict)
                Assert.Null(selfDict["Self"]);
        }
    }
}
