using EchoPhase.Types.Extensions;
using EchoPhase.Interfaces;
using EchoPhase.Requirements;

namespace EchoPhase.Factories
{
    public class RolesFactory : IRolesFactory
    {
        private readonly IRolesBitMaskService _rolesBitmaskService;

        public RolesFactory(IRolesBitMaskService rolesBitmaskService)
        {
            _rolesBitmaskService = rolesBitmaskService;
        }

        public RolesRequirement Requirement(params string[] roles)
        {
            var result = _rolesBitmaskService.Encode(roles);

            result.OnFailure(err =>
                throw new InvalidOperationException(err.Value));

            if (!result.TryGetValue(out var bitmask))
                throw new InvalidOperationException("Missing data.");

            return new RolesRequirement(bitmask, roles);
        }
    }
}
