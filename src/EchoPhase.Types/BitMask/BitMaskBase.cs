// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Collections.Concurrent;
using System.Numerics;

namespace EchoPhase.Types.BitMask
{
    public class BitMaskBase : IBitMask
    {
        private readonly ConcurrentDictionary<string, int> _map = new();
        private int _nextBit = 0;

        public BitMaskBase()
        {
        }

        public BitMaskBase(params IEnumerable<string> values)
        {
            foreach (var v in values)
                GetOrRegisterBit(v);
        }

        public BitArray Add(BitArray bitmask, bool registerIfMissing, params string[] values)
        {
            var result = Clone(bitmask);

            foreach (var v in values)
            {
                if (!_map.TryGetValue(v, out int bit))
                {
                    if (!registerIfMissing)
                        continue;

                    bit = GetOrRegisterBit(v);
                }

                EnsureLength(result, bit + 1);
                result.Set(bit, true);
            }

            return result;
        }

        public BitArray Add(BitArray bitmask, params string[] values) =>
            Add(bitmask, registerIfMissing: false, values);

        public bool ContainsKey(BitArray bitmask, string key)
        {
            if (!_map.TryGetValue(key, out int bit))
                return false;

            return bit < bitmask.Length && bitmask.Get(bit);
        }

        public BitArray Remove(BitArray bitmask, params string[] values)
        {
            var result = Clone(bitmask);
            foreach (var v in values)
            {
                if (_map.TryGetValue(v, out int bit) && bit < result.Length)
                    result.Set(bit, false);
            }
            return result;
        }

        public bool Has(BitArray bitmask, params string[] values)
        {
            foreach (var v in values)
            {
                if (_map.TryGetValue(v, out int bit))
                {
                    if (bit >= bitmask.Length || !bitmask.Get(bit))
                        return false;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        public BitArray Mask
        {
            get
            {
                var array = new BitArray(_nextBit);
                array.SetAll(true);
                return array;
            }
        }

        public IEnumerable<string> GetFlags(BitArray bitmask)
        {
            int index = 0;
            while (true)
            {
                int next = FindNextSetBit(bitmask, index);
                if (next == -1) break;
                index = next + 1;

                var flag = _map.FirstOrDefault(kv => kv.Value == next).Key;
                if (flag != null)
                    yield return flag;
            }
        }

        public bool IsRegistered(params string[] keys)
        {
            foreach (var key in keys)
                if (!_map.ContainsKey(key))
                    return false;
            return true;
        }

        public IEnumerable<string> Keys =>
            _map.OrderBy(kv => kv.Value).Select(kv => kv.Key);

        public static BitArray Empty =>
            new BitArray(0);

        private static int FindNextSetBit(BitArray bits, int startIndex)
        {
            int wordIndex = startIndex / 32;
            int bitIndex = startIndex % 32;

            for (int i = wordIndex; i < (bits.Length + 31) / 32; i++)
            {
                int word = GetWord(bits, i);
                if (i == wordIndex)
                    word &= ~((1 << bitIndex) - 1); // mask bits below startIndex

                if (word != 0)
                    return i * 32 + BitOperations.TrailingZeroCount((uint)word);
            }

            return -1;
        }

        private static int GetWord(BitArray bits, int wordIndex)
        {
            int start = wordIndex * 32;
            int word = 0;
            for (int i = 0; i < 32 && start + i < bits.Length; i++)
                if (bits.Get(start + i)) word |= (1 << i);
            return word;
        }

        private int GetOrRegisterBit(string key)
        {
            return _map.GetOrAdd(key, _ =>
            {
                int newBit = Interlocked.Increment(ref _nextBit) - 1;
                return newBit;
            });
        }

        private static void EnsureLength(BitArray bits, int minLength)
        {
            if (bits.Length < minLength)
                bits.Length = minLength;
        }

        private static BitArray Clone(BitArray source) => new(source);
    }
}
