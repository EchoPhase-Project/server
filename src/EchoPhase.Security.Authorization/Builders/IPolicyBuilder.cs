using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Builders
{
    public interface IPolicyBuilder
    {
        AuthorizationPolicy? Build(string policyBody);
    }
}
