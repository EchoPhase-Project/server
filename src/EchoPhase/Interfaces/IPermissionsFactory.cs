using EchoPhase.Enums;
using EchoPhase.Requirements;

namespace EchoPhase.Interfaces
{
    public interface IPermissionsFactory
    {
        PermissionsRequirement Requirement(params (Resources resource, string[] perms)[] resourcePerms);
    }
}
