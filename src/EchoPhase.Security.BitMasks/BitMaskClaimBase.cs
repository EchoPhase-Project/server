using System.Collections;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Security.BitMasks.Extensions;
using EchoPhase.Types.BitMask;
using EchoPhase.Types.Extensions;
using EchoPhase.Types.Result;

namespace EchoPhase.Security.BitMasks
{
    public abstract class BitMaskClaimBase<TConstants> : BitMaskBase
        where TConstants : ConstantsBase<TConstants>
    {
        public abstract string ClaimName { get; }

        private readonly Lazy<byte[]> _versionCache;
        public ReadOnlySpan<byte> Version => _versionCache.Value;

        protected BitMaskClaimBase()
            : base(ConstantsBase<TConstants>.AsEnumerable().Order())
        {
            _versionCache = new Lazy<byte[]>(() =>
            {
                var str = string.Join(';', ConstantsBase<TConstants>
                    .AsEnumerable()
                    .Order());
                return str.ComputeXxHash3();
            });
        }

        public IServiceResult<BitArray> Encode(string[] values)
        {
            if (values is { Length: 0 })
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("InvalidArguments", $"{ClaimName} input is null or empty."));

            if (!IsRegistered(values))
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("InvalidOperation", "Usage of unsupported values."));

            return ServiceResult<BitArray>.Success(Add(Empty, values));
        }

        public IServiceResult<string[]> Decode(BitArray bitmask)
        {
            if (bitmask is { Count: 0 })
                return ServiceResult<string[]>.Failure(err =>
                    err.Set("InvalidArguments", $"{ClaimName} bitmask is null or empty."));

            return ServiceResult<string[]>.Success(GetFlags(bitmask).ToArray());
        }

        public IServiceResult<string> Serialize(BitArray bitmask)
        {
            if (bitmask is { Count: 0 })
                return ServiceResult<string>.Failure(err =>
                    err.Set("InvalidArguments", $"{ClaimName} bitmask is null or empty."));

            return ServiceResult<string>.Success(
                $"{Convert.ToBase64String(Version)}${bitmask.ToBase64String()}");
        }

        public IServiceResult<BitArray> Deserialize(string serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized))
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("InvalidArguments", "Serialized value is null or empty."));

            var parts = serialized.Trim().Split('$', 2);
            if (parts.Length != 2)
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("InvalidVersion", "Unable to parse version."));

            byte[] versionBytes;
            try { versionBytes = Convert.FromBase64String(parts[0]); }
            catch (FormatException)
            {
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("InvalidVersion", "Unable to parse version."));
            }

            if (!versionBytes.AsSpan().SequenceEqual(Version))
                return ServiceResult<BitArray>.Failure(err =>
                    err.Set("VersionMismatch", "Version mismatch."));

            return ServiceResult<BitArray>.Success(parts[1].ToBitArray());
        }
    }
}
