using EchoPhase.Security.Authorization.Requirements;
using EchoPhase.Security.BitMasks.Constants;

namespace EchoPhase.Security.Authorization.Factories
{
    public interface IPermissionsFactory
    {
        PermissionsRequirement Requirement(params (Resources resource, string[] perms)[] resourcePerms);
    }
}
