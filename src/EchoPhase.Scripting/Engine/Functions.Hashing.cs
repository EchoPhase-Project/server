// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Buffers.Binary;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace EchoPhase.Scripting.Engine
{
    // Hashes, base64 and Bloom filters. Values are hashed as their UTF-8
    // text (str), so 1 and '1' hash alike, as they compare equal; objects and
    // lists as their JSON. Hashes are lowercase hex.
    //
    // A Bloom filter is a plain object, so it stores, prints and travels as
    // JSON like any value: { type: 'bloom', bits, hashes, count, data } with
    // data the bit array in base64. Like every value it is never changed in
    // place: bloomAdd returns a new filter.
    internal static partial class Functions
    {
        public const string BloomType = "bloom";

        /// <summary>1 Mbit (128 KiB): ~100k items at 1%, still well inside a run's result.</summary>
        public const int MaxBloomBits = 1 << 20;

        // Decoded bit arrays by their base64 text: a filter read in a loop is decoded once.
        private static readonly ConditionalWeakTable<string, byte[]> Decoded = new();

        private static readonly Dictionary<string, Definition> Hashing = new(StringComparer.OrdinalIgnoreCase)
        {
            // xxhash3(value, seed?) -> 16 hex digits (64 bits)
            ["xxhash3"] = new(1, 2, a => Hex(XxHash3.HashToUInt64(Utf8(a[0]), a.Length > 1 ? (long)Values.ToNumber(a[1]) : 0))),
            ["sha256"] = new(1, 1, a => Convert.ToHexStringLower(SHA256.HashData(Utf8(a[0])))),
            // Not for security; for checksums and APIs that ask for it.
            ["md5"] = new(1, 1, a => Convert.ToHexStringLower(MD5.HashData(Utf8(a[0])))),
            ["base64"] = new(1, 1, a => Convert.ToBase64String(Utf8(a[0]))),
            // Back to text (UTF-8); also reads the URL-safe alphabet and missing padding.
            ["fromBase64"] = new(1, 1, a => Encoding.UTF8.GetString(FromBase64(Values.Format(a[0])))),

            // bloom(capacity, errorRate = 0.01): an empty filter sized for that many items.
            ["bloom"] = new(1, 2, a => NewBloom(Values.ToNumber(a[0]), a.Length > 1 ? Values.ToNumber(a[1]) : 0.01)),
            // bloomFrom(list, errorRate = 0.01): a filter sized for the list, holding it.
            ["bloomFrom"] = new(1, 2, a =>
            {
                var items = Items(a, "bloomFrom").ToList();
                return Add(Bloom(NewBloom(Math.Max(1, items.Count), a.Length > 1 ? Values.ToNumber(a[1]) : 0.01), "bloomFrom"), items);
            }),
            // bloomAdd(filter, value) or bloomAdd(filter, [values]): a new filter with them too.
            ["bloomAdd"] = new(2, 2, a => Add(Bloom(a[0], "bloomAdd"), Values.Unwrap(a[1]) is IList<object?> list ? list : [a[1]])),
            // bloomHas(filter, value): false is certain; true is "probably" (the error rate).
            ["bloomHas"] = new(2, 2, a =>
            {
                var filter = Bloom(a[0], "bloomHas");
                var bits = BitsOf(filter);
                foreach (var bit in Positions(a[1], filter.Bits, filter.Hashes))
                    if ((bits[bit >> 3] & (1 << (bit & 7))) == 0)
                        return false;
                return true;
            }),
        };

        private static byte[] Utf8(object? value) => Encoding.UTF8.GetBytes(Values.Format(value));

        private static string Hex(ulong value) => value.ToString("x16");

        private static byte[] FromBase64(string text)
        {
            var normal = text.Trim().Replace('-', '+').Replace('_', '/');
            normal = normal.PadRight(normal.Length + (4 - normal.Length % 4) % 4, '=');
            try
            {
                return Convert.FromBase64String(normal);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("'fromBase64' got text that is not base64.");
            }
        }

        private readonly record struct BloomFilter(int Bits, int Hashes, long Count, string Data);

        // Optimal size for n items at error rate p: m = -n ln p / (ln 2)^2, k = (m / n) ln 2.
        private static Dictionary<string, object?> NewBloom(double capacity, double errorRate)
        {
            if (capacity < 1 || double.IsNaN(capacity))
                throw new InvalidOperationException("A Bloom filter needs a capacity of at least 1.");
            if (!(errorRate > 0 && errorRate < 1))
                throw new InvalidOperationException("A Bloom filter's error rate must be between 0 and 1, e.g. 0.01.");

            var bits = Math.Ceiling(-capacity * Math.Log(errorRate) / (Math.Log(2) * Math.Log(2)));
            if (bits > MaxBloomBits)
                throw new InvalidOperationException(
                    $"A Bloom filter for {Values.Format(capacity)} items at {Values.Format(errorRate)} needs {Values.Format(bits)} bits; at most {MaxBloomBits} are allowed.");

            int m = Math.Max(64, (int)bits + 7 & ~7);
            int k = Math.Clamp((int)Math.Round(m / capacity * Math.Log(2)), 1, 30);
            Budget.Spend(m / 64);
            return Shape(new BloomFilter(m, k, 0, Convert.ToBase64String(new byte[m / 8])));
        }

        private static Dictionary<string, object?> Shape(BloomFilter filter) => new()
        {
            ["type"] = BloomType,
            ["bits"] = filter.Bits,
            ["hashes"] = filter.Hashes,
            ["count"] = filter.Count,
            ["data"] = filter.Data
        };

        private static BloomFilter Bloom(object? value, string name)
        {
            if (Values.Unwrap(value) is IDictionary<string, object?> d
                && d.TryGetValue("type", out var type) && type as string == BloomType
                && d.TryGetValue("bits", out var bits) && Values.TryNumber(bits, out var m)
                && d.TryGetValue("hashes", out var hashes) && Values.TryNumber(hashes, out var k)
                && d.TryGetValue("data", out var data) && data is string text
                && m is >= 64 and <= MaxBloomBits && m % 8 == 0 && k is >= 1 and <= 30
                && text.Length == (int)(4 * Math.Ceiling(m / 8 / 3.0)))
            {
                var count = d.TryGetValue("count", out var c) && Values.TryNumber(c, out var n) ? (long)n : 0;
                return new BloomFilter((int)m, (int)k, count, text);
            }

            throw new InvalidOperationException($"'{name}' needs a Bloom filter made by bloom() or bloomFrom().");
        }

        private static byte[] BitsOf(BloomFilter filter)
            => Decoded.GetValue(filter.Data, static data =>
            {
                try
                {
                    return Convert.FromBase64String(data);
                }
                catch (FormatException)
                {
                    throw new InvalidOperationException("The Bloom filter's data is damaged.");
                }
            });

        private static Dictionary<string, object?> Add(BloomFilter filter, IList<object?> items)
        {
            var bits = (byte[])BitsOf(filter).Clone();
            Budget.Spend(filter.Bits / 64);
            foreach (var item in items)
            {
                Budget.Spend(filter.Hashes);
                foreach (var bit in Positions(item, filter.Bits, filter.Hashes))
                    bits[bit >> 3] |= (byte)(1 << (bit & 7));
            }

            var data = Convert.ToBase64String(bits);
            Decoded.AddOrUpdate(data, bits);
            return Shape(filter with { Count = filter.Count + items.Count, Data = data });
        }

        // Kirsch–Mitzenmacher: the halves of one 64-bit XXH3 give every probe, h1 + i * h2 (mod m).
        private static IEnumerable<int> Positions(object? value, int bits, int hashes)
        {
            Span<byte> hash = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(hash, XxHash3.HashToUInt64(Utf8(value)));
            ulong h1 = BinaryPrimitives.ReadUInt32LittleEndian(hash);
            ulong h2 = BinaryPrimitives.ReadUInt32LittleEndian(hash[4..]) | 1;

            var positions = new int[hashes];
            for (int i = 0; i < hashes; i++)
                positions[i] = (int)((h1 + (ulong)i * h2) % (ulong)bits);
            return positions;
        }
    }
}
