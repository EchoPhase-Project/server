using System.Collections;
using EchoPhase.Enums;
using EchoPhase.Interfaces;
using EchoPhase.Requirements;
using EchoPhase.Services.Bitmasks;

namespace EchoPhase.Factories
{
    public class PermissionsFactory : IPermissionsFactory
    {
        private readonly IPermissionsBitMaskService _permissionsBitmaskService;

        public PermissionsFactory(IPermissionsBitMaskService permissionsBitmaskService)
        {
            _permissionsBitmaskService = permissionsBitmaskService;
        }

        public PermissionsRequirement Requirement(params (Resources resource, string[] perms)[] resourcePerms)
        {
            var dict = new Dictionary<Resources, BitArray>();

            foreach (var (resource, perms) in resourcePerms)
            {
                if (!Enum.IsDefined(typeof(Resources), resource) || perms is null || perms.Length == 0)
                    throw new InvalidOperationException("Invalid data state.");

                if (!_permissionsBitmaskService.IsRegistered(perms))
                    throw new InvalidOperationException("Usage of unsupported values.");

                var current = dict.GetValueOrDefault(resource) ?? BitMaskServiceBase.Empty;
                var updated = _permissionsBitmaskService.Add(current, false, perms);
                dict[resource] = updated;
            }

            return new PermissionsRequirement(dict, resourcePerms.ToDictionary());
        }
    }
}
