using EchoPhase.Security.Authorization.Requirements;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Result.Extensions;

namespace EchoPhase.Security.Authorization.Factories
{
    public class RolesFactory : IRolesFactory
    {
        private readonly IRolesBitMask _rolesBitmask;

        public RolesFactory(IRolesBitMask rolesBitmask)
        {
            _rolesBitmask = rolesBitmask;
        }

        public RolesRequirement Requirement(params string[] roles)
        {
            var result = _rolesBitmask.Encode(roles);

            result.OnFailure(err =>
                throw new InvalidOperationException(err.Value));

            if (!result.TryGetValue(out var bitmask))
                throw new InvalidOperationException("Missing data.");

            return new RolesRequirement(bitmask, roles);
        }
    }
}
