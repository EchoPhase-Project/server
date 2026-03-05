using System.Collections;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Security.BitMasks.Extensions;
using EchoPhase.Types.BitMask;
using EchoPhase.Types.Extensions;
using EchoPhase.Types.Result;
using EchoPhase.Security.BitMasks.Comparers;

namespace EchoPhase.Security.BitMasks
{
    public abstract class DualBitMaskBase<TKey, TValue> : BitMaskBase
        where TKey   : ConstantsBase<TKey>
        where TValue : ConstantsBase<TValue>
    {
        public abstract string ClaimName { get; }
        public ReadOnlySpan<byte> Version => _versionCache.Value;

        public static BitMaskBase KeyMask { get; } =
            new BitMaskBase(ConstantsBase<TKey>.AsEnumerable().Order());

        private readonly Lazy<byte[]> _versionCache;

        protected DualBitMaskBase()
            : base(ConstantsBase<TValue>.AsEnumerable().Order())
        {
            _versionCache = new Lazy<byte[]>(() =>
            {
                var keyString = string.Join(';', ConstantsBase<TKey>.AsEnumerable().Order());
                var valString = string.Join(';', ConstantsBase<TValue>.AsEnumerable().Order());

                var hashKey = keyString.ComputeXxHash3();
                var hashVal = valString.ComputeXxHash3();

                Span<byte> buffer = stackalloc byte[16];
                hashKey.CopyTo(buffer[..hashKey.Length]);
                hashVal.CopyTo(buffer[hashKey.Length..]);

                return buffer.ComputeXxHash3().ToArray();
            });
        }

        // ── Encode ────────────────────────────────────────────────────────────

        public IServiceResult<IReadOnlyDictionary<BitArray, BitArray>> Encode(
            IReadOnlyDictionary<string, string[]> dict)
        {
            if (dict is { Count: 0 })
                return Fail<IReadOnlyDictionary<BitArray, BitArray>>(
                    "InvalidArguments", "Dictionary input is null or empty.");

            var result = new Dictionary<BitArray, BitArray>(BitArrayComparer.Instance);

            foreach (var (key, values) in dict)
            {
                if (string.IsNullOrEmpty(key))
                    return Fail<IReadOnlyDictionary<BitArray, BitArray>>(
                        "InvalidArguments", "Key is null or empty.");

                if (values is null || values.Length == 0)
                    return Fail<IReadOnlyDictionary<BitArray, BitArray>>(
                        "InvalidArguments", "Values array is null or empty.");

                if (!KeyMask.IsRegistered([key]))
                    return Fail<IReadOnlyDictionary<BitArray, BitArray>>(
                        "InvalidOperation", $"Unknown key '{key}'.");

                if (!IsRegistered(values))
                    return Fail<IReadOnlyDictionary<BitArray, BitArray>>(
                        "InvalidOperation", $"Unknown value in [{string.Join(", ", values)}].");

                var keyBits = KeyMask.Add(BitMaskBase.Empty, [key]);
                var valBits = Add(BitMaskBase.Empty, values);

                result[keyBits] = result.TryGetValue(keyBits, out var existing)
                    ? new BitArray(existing).Or(valBits)
                    : valBits;
            }

            return ServiceResult<IReadOnlyDictionary<BitArray, BitArray>>.Success(result);
        }

        // ── Decode ────────────────────────────────────────────────────────────

        public IServiceResult<IReadOnlyDictionary<string, string[]>> Decode(
            IReadOnlyDictionary<BitArray, BitArray> bitmasks)
        {
            if (bitmasks is { Count: 0 })
                return Fail<IReadOnlyDictionary<string, string[]>>(
                    "InvalidArguments", "Bitmask input is null or empty.");

            var result = new Dictionary<string, string[]>();

            foreach (var (keyBits, valBits) in bitmasks)
            {
                var keys = KeyMask.GetFlags(keyBits).ToArray();

                // Каждый бит ключа — отдельная запись, values общие для всей группы
                var values = GetFlags(valBits).ToArray();

                foreach (var key in keys)
                {
                    result[key] = result.TryGetValue(key, out var existing)
                        ? existing.Union(values).ToArray()
                        : values;
                }
            }

            return ServiceResult<IReadOnlyDictionary<string, string[]>>.Success(result);
        }

        // ── Serialize / Deserialize — без изменений ───────────────────────────
        // (работают с IReadOnlyDictionary<BitArray, BitArray> — не затронуты)

        public IServiceResult<string> Serialize(
            IReadOnlyDictionary<BitArray, BitArray> dict)
        {
            if (dict is { Count: 0 })
                return Fail<string>("InvalidArguments", "Dictionary input is null or empty.");

            var body = string.Join(";", dict.Select(kv =>
                $"{kv.Key.ToBase64String()}:{kv.Value.ToBase64String()}"));

            return ServiceResult<string>.Success(
                $"{Convert.ToBase64String(Version)}${body}");
        }

        public IServiceResult<IReadOnlyDictionary<BitArray, BitArray>> Deserialize(
            string serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized))
                return Fail<IReadOnlyDictionary<BitArray, BitArray>>(
                    "InvalidArguments", "Serialized value is null or empty.");

            var top = serialized.Trim().Split('$', 2);
            if (top.Length != 2)
                return Fail<IReadOnlyDictionary<BitArray, BitArray>>(
                    "InvalidVersion", "Unable to parse version.");

            byte[] versionBytes;
            try { versionBytes = Convert.FromBase64String(top[0]); }
            catch (FormatException)
            {
                return Fail<IReadOnlyDictionary<BitArray, BitArray>>(
                    "InvalidVersion", "Unable to parse version.");
            }

            if (!versionBytes.AsSpan().SequenceEqual(Version))
                return Fail<IReadOnlyDictionary<BitArray, BitArray>>(
                    "VersionMismatch", "Version mismatch.");

            var result = new Dictionary<BitArray, BitArray>(BitArrayComparer.Instance);

            foreach (var entry in top[1].Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split(':', 2);
                if (parts.Length != 2)
                    return Fail<IReadOnlyDictionary<BitArray, BitArray>>(
                        "InvalidData", $"Invalid entry '{entry}'.");

                var keyBits = parts[0].ToBitArray();
                var valBits = parts[1].ToBitArray();

                result[keyBits] = result.TryGetValue(keyBits, out var existing)
                    ? new BitArray(existing).Or(valBits)
                    : valBits;
            }

            return ServiceResult<IReadOnlyDictionary<BitArray, BitArray>>.Success(result);
        }

        private static IServiceResult<T> Fail<T>(string code, string message) =>
            ServiceResult<T>.Failure(err => err.Set(code, message));
    }
}
