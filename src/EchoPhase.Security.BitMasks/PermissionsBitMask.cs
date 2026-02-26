using System.Collections;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Security.BitMasks.Extensions;
using EchoPhase.Types.BitMask;
using EchoPhase.Types.Extensions;
using EchoPhase.Types.Result;

namespace EchoPhase.Security.BitMasks
{
    public class PermissionsBitMask : BitMaskBase, IPermissionsBitMask
    {
        public const string ClaimName = "bmperm";

        private static readonly Lazy<byte[]> _versionCache = new(() =>
        {
            string enumString = string.Join(";", Enum.GetValues(typeof(Resources))
                .Cast<Resources>()
                .OrderBy(v => Convert.ToInt32(v))
                .Select(v => $"{v}:{Convert.ToInt32(v)}"));

            string permissionsString = string.Join(';', Permissions
                .AsEnumerable()
                .Order());

            var hashEnum = enumString.ComputeXxHash3();
            var hashPermissions = permissionsString.ComputeXxHash3();

            Span<byte> buffer = stackalloc byte[16];
            hashEnum.CopyTo(buffer.Slice(0, hashEnum.Length));
            hashPermissions.CopyTo(buffer.Slice(hashEnum.Length, hashPermissions.Length));

            return buffer.ComputeXxHash3().ToArray();
        });

        public static ReadOnlySpan<byte> Version => _versionCache.Value;

        public PermissionsBitMask()
            : base(Permissions.AsEnumerable().Order()) { }

        public IServiceResult<IReadOnlyDictionary<Resources, BitArray>> Encode(
            IReadOnlyDictionary<Resources, string[]> dict)
        {
            if (dict is { Count: 0 })
                return ServiceResult<IReadOnlyDictionary<Resources, BitArray>>.Failure(err =>
                    err.Set("InvalidArguments", "Dictionary input is null or empty."));

            var masked = new Dictionary<Resources, BitArray>();

            foreach (var (resource, perms) in dict)
            {
                if (!Enum.IsDefined(typeof(Resources), resource) || perms is null || perms.Length == 0)
                    return ServiceResult<IReadOnlyDictionary<Resources, BitArray>>.Failure(err =>
                        err.Set("InvalidOperation", "Invalid data state."));

                if (!IsRegistered(perms))
                    return ServiceResult<IReadOnlyDictionary<Resources, BitArray>>.Failure(err =>
                        err.Set("InvalidOperation", "Usage of unsupported values."));

                var current = masked.GetValueOrDefault(resource) ?? BitMaskBase.Empty;
                var updated = Add(current, perms);
                masked[resource] = updated;
            }

            return ServiceResult<IReadOnlyDictionary<Resources, BitArray>>.Success(masked);
        }

        public IServiceResult<IReadOnlyDictionary<Resources, string[]>> Decode(IReadOnlyDictionary<Resources, BitArray> bitmasks)
        {
            if (bitmasks is { Count: 0 })
            {
                return ServiceResult<IReadOnlyDictionary<Resources, string[]>>.Failure(err =>
                    err.Set("InvalidArguments", "Bitmask input is null or empty."));
            }

            var decoded = new Dictionary<Resources, string[]>();

            foreach (var (resource, bitmask) in bitmasks)
            {
                if (!Enum.IsDefined(typeof(Resources), resource) || bitmask == null)
                {
                    return ServiceResult<IReadOnlyDictionary<Resources, string[]>>.Failure(err =>
                        err.Set("InvalidOperation", $"Invalid resource or bitmask for {resource}."));
                }

                var flags = GetFlags(bitmask).ToArray();
                decoded[resource] = flags;
            }

            return ServiceResult<IReadOnlyDictionary<Resources, string[]>>.Success(decoded);
        }

        public static IServiceResult<string> Serialize(IReadOnlyDictionary<Resources, BitArray> dict)
        {
            if (dict is { Count: 0 })
            {
                return ServiceResult<string>.Failure(err =>
                    err.Set("InvalidArguments", "Dictionary input is null or empty."));
            }

            var serialized = string.Join(";",
                dict.Select(kv =>
                    Format(kv.Key, kv.Value)));

            var result = $"{Convert.ToBase64String(Version)}${serialized}";

            return ServiceResult<string>.Success(result);
        }

        public static IServiceResult<IReadOnlyDictionary<Resources, BitArray>> Deserialize(string serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized))
                return ServiceResult<IReadOnlyDictionary<Resources, BitArray>>.Failure(err =>
                    err.Set("InvalidArguments", "Serialized value is null or empty."));

            var dict = new Dictionary<Resources, BitArray>();

            if (!TryGetVersionBytes(serialized.Trim(), out var versionBytes, out var data))
                return ServiceResult<IReadOnlyDictionary<Resources, BitArray>>.Failure(err =>
                    err.Set("InvalidVersion", "Unable to parse version."));

            if (!versionBytes.SequenceEqual(Version))
                return ServiceResult<IReadOnlyDictionary<Resources, BitArray>>.Failure(err =>
                    err.Set("VersionMismatch", "Version mismatch."));

            var entries = data.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split(':', 2);
                if (parts.Length != 2)
                    return ServiceResult<IReadOnlyDictionary<Resources, BitArray>>.Failure(err =>
                        err.Set("InvalidData", $"Invalid entry '{entry}'."));

                if (!Enum.TryParse(parts[0], out Resources resource))
                    return ServiceResult<IReadOnlyDictionary<Resources, BitArray>>.Failure(err =>
                        err.Set("InvalidData", $"Invalid resource '{parts[0]}'."));

                var bits = parts[1].ToBitArray();

                if (dict.TryGetValue(resource, out var existing))
                {
                    var combined = new BitArray(existing);
                    combined.Or(bits);
                    dict[resource] = combined;
                }
                else
                {
                    dict[resource] = bits;
                }
            }

            return ServiceResult<IReadOnlyDictionary<Resources, BitArray>>.Success(dict);
        }

        private static bool TryGetVersionBytes(
            string serialized,
            out byte[] versionBytes,
            out string data)
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

        private static string Format(Resources resource, BitArray bitmask) =>
            $"{(int)resource}:{bitmask.ToBase64String()}";
    }
}
