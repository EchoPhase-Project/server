using System.Collections;
using EchoPhase.Constants;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;
using EchoPhase.Services.Results;

namespace EchoPhase.Services.Bitmasks
{
    public class RolesBitMaskService : BitMaskServiceBase, IRolesBitMaskService
    {
        public const string ClaimName = "bmrole";

        public static ReadOnlySpan<byte> Version
        {
            get
            {
                string rolesString = string.Join(';', Intents
                    .AsEnumerable()
                    .Order());

                return rolesString.ComputeXxHash3();
            }
        }

        public RolesBitMaskService()
            : base(Roles.AsEnumerable().Order()) { }

        public IServiceResult<BitArray> Encode(string[] roles)
        {
            if (roles is { Length: 0 })
            {
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("InvalidArguments", "Dictionary input is null or empty."));
            }

            var mask = Empty;

            if (!IsRegistered(roles))
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("InvalidOperation", "Usage of unsupported values."));

            mask = Add(mask, roles);

            return ServiceResult<BitArray>.Success(mask);
        }

        public IServiceResult<string[]> Decode(BitArray bitmask)
        {
            if (bitmask is { Count: 0 })
            {
                return ServiceResult<string[]>.Failure(err =>
                    err.Set("InvalidArguments", "Bitmask input is null or empty."));
            }

            var flags = GetFlags(bitmask).ToArray();

            return ServiceResult<string[]>.Success(flags);
        }

        public static IServiceResult<string> Serialize(BitArray roles)
        {
            var serialized = $"{Convert.ToBase64String(Version)}${roles.ToBase64String()}";
            return ServiceResult<string>.Success(serialized);
        }

        public static IServiceResult<BitArray> Deserialize(string serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized))
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("InvalidArguments", "Serialized value is null or empty."));

            if (!TryGetVersionBytes(serialized.Trim(), out var versionBytes, out var data))
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("InvalidVersion", "Unable to parse version."));

            if (!versionBytes.SequenceEqual(Version))
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("VersionMismatch", "Version mismatch."));

            return ServiceResult<BitArray>.Success(data.ToBitArray());
        }

        private static bool TryGetVersionBytes(string serialized, out ReadOnlySpan<byte> versionBytes, out string data)
        {
            versionBytes = null;
            data = string.Empty;

            var parts = serialized.Split('$', 2);
            if (parts.Length != 2)
                return false;

            try
            {
                versionBytes = Convert.FromBase64String(parts[0]);
                data = parts[1];
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
