using System.Collections;
using System.Text.Json;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Result.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Security.Authorization.Requirements
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
            var res2 = RolesBitMask.Deserialize(serializedRolesBitmasks);

            res2.OnFailure(err =>
                throw new InvalidOperationException(err.Value));

            if (!res2.TryGetValue(out var dict) || res1 == null)
                throw new InvalidOperationException($"Missing parse value.");

            Roles = res1;
            RolesBitmasks = dict;
        }

        public string SerializeBitmasks()
        {
            var res = RolesBitMask.Serialize(RolesBitmasks);

            res.OnFailure(err =>
                throw new InvalidOperationException(err.Value));

            if (!res.TryGetValue(out var result))
                throw new NullReferenceException($"Missing result from {typeof(RolesBitMask).Name}.");

            return result;
        }

        public string Serialize() =>
            JsonSerializer.Serialize(Roles);
    }
}
