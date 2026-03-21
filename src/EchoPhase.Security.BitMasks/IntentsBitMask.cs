// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.BitMasks.Constants;

namespace EchoPhase.Security.BitMasks
{
    public class IntentsBitMask : BitMaskClaimBase<Intents>
    {
        public override string ClaimName => "bmintents";
    }
}
