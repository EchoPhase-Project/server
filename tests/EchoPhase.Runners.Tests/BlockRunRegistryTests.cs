// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Runs;

namespace EchoPhase.Runners.Tests
{
    public class BlockRunRegistryTests
    {
        private readonly BlockRunRegistry _registry = new();
        private readonly Guid _alice = Guid.NewGuid();
        private readonly Guid _bob = Guid.NewGuid();

        [Fact]
        public void PerUserLimit_IsEnforcedAndFreedOnRelease()
        {
            var first = Guid.NewGuid();
            Assert.NotNull(_registry.TryReserve(_alice, first, maxPerUser: 2));
            Assert.NotNull(_registry.TryReserve(_alice, Guid.NewGuid(), maxPerUser: 2));
            Assert.Null(_registry.TryReserve(_alice, Guid.NewGuid(), maxPerUser: 2));

            // Other users are not affected by Alice's limit.
            Assert.NotNull(_registry.TryReserve(_bob, Guid.NewGuid(), maxPerUser: 2));

            _registry.Release(first);
            Assert.NotNull(_registry.TryReserve(_alice, Guid.NewGuid(), maxPerUser: 2));
        }

        [Fact]
        public void Cancel_SignalsTheRunsToken()
        {
            var runId = Guid.NewGuid();
            var token = _registry.TryReserve(_alice, runId, maxPerUser: 1)!.Value;

            Assert.True(_registry.TryCancel(_alice, runId));
            Assert.True(token.IsCancellationRequested);
        }

        [Fact]
        public void Cancel_ByAnotherUser_LooksLikeAnUnknownRun()
        {
            var runId = Guid.NewGuid();
            var token = _registry.TryReserve(_alice, runId, maxPerUser: 1)!.Value;

            Assert.False(_registry.TryCancel(_bob, runId));
            Assert.False(_registry.TryCancel(_alice, Guid.NewGuid()));
            Assert.False(token.IsCancellationRequested);
        }

        [Fact]
        public void ReleasedRun_IsNoLongerActive()
        {
            var runId = Guid.NewGuid();
            _registry.TryReserve(_alice, runId, maxPerUser: 1);

            _registry.Release(runId);

            Assert.False(_registry.IsActive(runId));
            Assert.False(_registry.TryCancel(_alice, runId));
        }
    }
}
