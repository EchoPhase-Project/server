using EchoPhase.Security.BitMasks.Constants;

namespace EchoPhase.Security.BitMasks
{
    public class ScopesBitMask : BitMaskClaimBase<Scopes>
    {
        public override string ClaimName => "bmscope";
    }
}
