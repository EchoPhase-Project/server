using EchoPhase.Security.Authorization.Requirements;

namespace EchoPhase.Security.Authorization.Factories
{
    public interface IRolesFactory
    {
        RolesRequirement Requirement(params string[] roles);
    }
}
