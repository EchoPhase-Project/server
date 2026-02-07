using System.Collections;
using System.Text.Json;
using EchoPhase.Types.Extensions;
using EchoPhase.Services.Bitmasks;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Requirements
{
    public class RolesRequirement : IAuthorizationRequirement
    {
        public string[] Roles
        {
            get;
        }
        public BitArray RolesBitmasks
        {
            get;
        }

        public RolesRequirement(
            BitArray rolesBitmasks,
            string[] roles)
        {
            Roles = roles;
            RolesBitmasks = rolesBitmasks;
        }

        public RolesRequirement(
            string serializedRolesBitmasks,
            string serializedRoles)
        {
            var res1 = JsonSerializer.Deserialize<string[]>(serializedRoles);
            var res2 = RolesBitMaskService.Deserialize(serializedRolesBitmasks);

            res2.OnFailure(err =>
                throw new InvalidOperationException(err.Value));

            if (!res2.TryGetValue(out var dict) || res1 == null)
                throw new InvalidOperationException($"Missing parse value.");

            Roles = res1;
            RolesBitmasks = dict;
        }

        public string SerializeBitmasks()
        {
            var res = RolesBitMaskService.Serialize(RolesBitmasks);

            res.OnFailure(err =>
                throw new InvalidOperationException(err.Value));

            if (!res.TryGetValue(out var result))
                throw new NullReferenceException($"Missing result from {typeof(RolesBitMaskService).Name}.");

            return result;
        }

        public string Serialize() =>
            JsonSerializer.Serialize(Roles);
    }
}
