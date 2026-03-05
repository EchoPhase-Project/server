using EchoPhase.Security.BitMasks.Constants;

namespace EchoPhase.Security.BitMasks
{
    public class ResourcePermissionsBitMask
        : DualBitMaskBase<Resources, Permissions>
    {
        public override string ClaimName => "bmperm";
    }
}
