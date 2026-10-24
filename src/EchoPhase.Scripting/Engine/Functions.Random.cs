// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Buffers.Binary;
using System.Security.Cryptography;
using UUIDNext;

namespace EchoPhase.Scripting.Engine
{
    // Randomness, all from the cryptographic generator: fast enough for any
    // program, and safe for tokens, nonces and ids without a second thought.
    internal static partial class Functions
    {
        public const int MaxRandomBytes = 1024;

        private static readonly Dictionary<string, Definition> Randomness = new(StringComparer.OrdinalIgnoreCase)
        {
            // [0, 1), 53 random bits: every double of the form k / 2^53.
            ["rand"] = new(0, 0, _ => Unit()),
            // randInt(a, b): a whole number from a to b, both included (Python's randint).
            ["randInt"] = new(2, 2, a => (double)RandInt(a[0], a[1])),
            // A random element; null for an empty list.
            ["choice"] = new(1, 1, a => Items(a, "choice").ToList() is { Count: > 0 } items
                ? items[RandomNumberGenerator.GetInt32(items.Count)]
                : null),
            // A shuffled copy (Fisher–Yates).
            ["shuffle"] = new(1, 1, a =>
            {
                var items = Items(a, "shuffle").ToList();
                RandomNumberGenerator.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(items));
                return items;
            }),
            // randomBytes(n, 'hex' | 'base64' | 'base64url'): n random bytes as text.
            ["randomBytes"] = new(1, 2, a =>
            {
                var count = Values.ToNumber(a[0]);
                if (count != Math.Floor(count) || count is < 1 or > MaxRandomBytes)
                    throw new InvalidOperationException($"'randomBytes' takes 1-{MaxRandomBytes} bytes.");
                var bytes = RandomNumberGenerator.GetBytes((int)count);
                return (a.Length > 1 ? Values.Format(a[1]).ToLowerInvariant() : "hex") switch
                {
                    "hex" => Convert.ToHexStringLower(bytes),
                    "base64" => Convert.ToBase64String(bytes),
                    "base64url" => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
                    var other => throw new InvalidOperationException($"Unknown encoding '{other}'; use hex, base64 or base64url.")
                };
            }),
            // Random (v4) and time-ordered (v7, sorts by creation time) UUIDs.
            ["uuid4"] = new(0, 0, _ => Uuid.NewRandom().ToString()),
            ["uuid7"] = new(0, 0, _ => Uuid.NewSequential().ToString()),
        };

        private static double Unit()
        {
            Span<byte> bytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(bytes);
            return (BinaryPrimitives.ReadUInt64LittleEndian(bytes) >> 11) * (1.0 / (1UL << 53));
        }

        private static long RandInt(object? low, object? high)
        {
            double a = Values.ToNumber(low), b = Values.ToNumber(high);
            if (a != Math.Floor(a) || b != Math.Floor(b))
                throw new InvalidOperationException("'randInt' takes whole numbers.");
            if (a > b)
                throw new InvalidOperationException($"'randInt' needs a <= b, not {Values.Format(a)} > {Values.Format(b)}.");
            if (a < int.MinValue || b >= int.MaxValue)
                throw new InvalidOperationException($"'randInt' bounds must be within {int.MinValue}..{int.MaxValue - 1}.");
            return RandomNumberGenerator.GetInt32((int)a, (int)b + 1);
        }
    }
}
