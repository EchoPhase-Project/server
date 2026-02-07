using System.Collections;
using System.Text.Json;
using EchoPhase.Enums;
using EchoPhase.Types.Extensions;
using EchoPhase.Services.Bitmasks;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Requirements
{
    public class PermissionsRequirement : IAuthorizationRequirement
    {
        public IReadOnlyDictionary<Resources, string[]> Permissions
        {
            get;
        }
        public IReadOnlyDictionary<Resources, BitArray> PermissionsBitmasks
        {
            get;
        }

        public PermissionsRequirement(
            IDictionary<Resources, BitArray> permissionsBitmasks,
            IDictionary<Resources, string[]> permissions)
        {
            Permissions = new Dictionary<Resources, string[]>(permissions);
            PermissionsBitmasks = new Dictionary<Resources, BitArray>(permissionsBitmasks);
        }

        public PermissionsRequirement(
            string serializedPermissionsBitmasks,
            string serializedPermissions)
        {
            var res1 = JsonSerializer.Deserialize<Dictionary<Resources, string[]>>(serializedPermissions);
            var res2 = PermissionsBitMaskService.Deserialize(serializedPermissionsBitmasks);

            res2.OnFailure(err =>
                throw new InvalidOperationException(err.Value));

            if (!res2.TryGetValue(out var dict) || res1 == null)
                throw new InvalidOperationException($"Missing parse value.");

            Permissions = res1;
            PermissionsBitmasks = dict;
        }

        public string SerializeBitmasks()
        {
            var res = PermissionsBitMaskService.Serialize(PermissionsBitmasks);

            res.OnFailure(err =>
                throw new InvalidOperationException(err.Value));

            if (!res.TryGetValue(out var result))
                throw new NullReferenceException($"Missing result from {typeof(PermissionsBitMaskService).Name}.");

            return result;
        }

        public string Serialize() =>
            JsonSerializer.Serialize(Permissions);
    }
}
