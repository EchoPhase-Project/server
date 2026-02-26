using System.Collections;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Security.BitMasks.Extensions;
using EchoPhase.Types.BitMask;
using EchoPhase.Types.Extensions;
using EchoPhase.Types.Result;

namespace EchoPhase.Security.BitMasks
{
    public class RolesBitMask : BitMaskBase, IRolesBitMask
    {
        public const string ClaimName = "bmrole";

        private static readonly Lazy<byte[]> _versionCache = new(() =>
        {
            string rolesString = string.Join(';', Intents
                .AsEnumerable()
                .Order());
            return rolesString.ComputeXxHash3();
        });

        public static ReadOnlySpan<byte> Version => _versionCache.Value;

        public RolesBitMask()
            : base(Roles.AsEnumerable().Order()) { }

        public IServiceResult<BitArray> Encode(string[] roles)
        {
            if (roles is { Length: 0 })
            {
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("InvalidArguments", "Roles input is null or empty."));
            }

            if (!IsRegistered(roles))
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("InvalidOperation", "Usage of unsupported values."));

            var mask = Add(Empty, roles);
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
            if (roles is { Count: 0 })
            {
                return ServiceResult<string>.Failure(err =>
                    err.Set("InvalidArguments", "Roles bitmask is null or empty."));
            }

            var serialized = Format(roles);
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

            if (!versionBytes.AsSpan().SequenceEqual(Version))
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("VersionMismatch", "Version mismatch."));

            return ServiceResult<BitArray>.Success(data.ToBitArray());
        }

        private static bool TryGetVersionBytes(string serialized, out byte[] versionBytes, out string data)
        {
            versionBytes = Array.Empty<byte>();
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

        private static string Format(BitArray roles) =>
            $"{Convert.ToBase64String(Version)}${roles.ToBase64String()}";
    }
}
