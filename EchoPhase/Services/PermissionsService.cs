using EchoPhase.Interfaces;

namespace EchoPhase.Services
{
    public class PermissionsService : BitMaskServiceBase, IPermissionsService
    {
        public static readonly string ClaimName = "Permissions";

        public PermissionsService()
            : base("Can Edit") { }
    }
}
