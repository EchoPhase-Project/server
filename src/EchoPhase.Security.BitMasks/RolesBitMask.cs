using EchoPhase.Security.BitMasks.Constants;

namespace EchoPhase.Security.BitMasks
{
    public class RolesBitMask : BitMaskClaimBase<Roles>
    {
        public override string ClaimName => "bmrole";
    }
}
