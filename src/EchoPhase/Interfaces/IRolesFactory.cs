using EchoPhase.Requirements;

namespace EchoPhase.Interfaces
{
    public interface IRolesFactory
    {
        RolesRequirement Requirement(params string[] roles);
    }
}
