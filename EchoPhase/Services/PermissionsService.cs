using EchoPhase.Interfaces;

namespace EchoPhase.Services
{
    public class PermissionsService : BitMaskServiceBase, IPermissionsService
    {
        public static readonly string ClaimName = "perm";

        public PermissionsService()
            : base("Can Edit") { }
    }
}
