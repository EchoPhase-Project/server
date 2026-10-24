// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// The stable orderings behind sort() and sortDesc(), as permutations of
    /// the input positions. Numbers: LSD radix sort (stable by construction),
    /// skipped when they are already in order. Text: TimSort, stable by
    /// construction and O(n log n) at worst, and fast on what programs mostly
    /// sort: data that is already partly in order (API results by date or
    /// id, lists appended to). Ascending: nulls first, then NaN, then numbers
    /// (-0 equals 0); descending is the exact reverse of that by key, with
    /// equal keys still in input order.
    /// </summary>
    internal static class Sorting
    {
        // Below this, insertion sort beats setting up radix passes or partitions.
        private const int Small = 32;

        public static int[] Numbers(double?[] keys, bool descending)
        {
            var order = new List<int>(keys.Length);
            var nulls = new List<int>();
            var bits = new List<ulong>(keys.Length);

            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] is not { } key)
                {
                    nulls.Add(i);
                    continue;
                }
                var ordered = OrderedBits(key);
                order.Add(i);
                bits.Add(descending ? ~ordered : ordered);
            }

            var sorted = InOrder(bits) ? [.. order]
                : order.Count < Small ? InsertionByBits(order, bits)
                : Radix(order, bits);
            return descending ? [.. sorted, .. nulls] : [.. nulls, .. sorted];
        }

        public static int[] Texts(string?[] keys, bool descending)
        {
            var entries = new List<Entry>(keys.Length);
            var nulls = new List<int>();
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] is { } key)
                    entries.Add(new Entry(key, i));
                else
                    nulls.Add(i);
            }

            var array = entries.ToArray();
            new TimSort(array, descending).Sort();

            var sorted = array.Select(e => e.Index);
            return descending ? [.. sorted, .. nulls] : [.. nulls, .. sorted];
        }

        // An unsigned key whose order is the double's: negatives flipped,
        // positives above them; -0 as 0, every NaN below -Infinity.
        private static ulong OrderedBits(double value)
        {
            if (double.IsNaN(value))
                return 0;
            if (value == 0)
                value = 0; // -0 -> 0
            var raw = (ulong)BitConverter.DoubleToInt64Bits(value);
            return (raw & 0x8000_0000_0000_0000UL) != 0 ? ~raw : raw | 0x8000_0000_0000_0000UL;
        }

        private static bool InOrder(List<ulong> bits)
        {
            for (int i = 1; i < bits.Count; i++)
                if (bits[i] < bits[i - 1])
                    return false;
            return true;
        }

        private static int[] InsertionByBits(List<int> order, List<ulong> bits)
        {
            var idx = order.ToArray();
            var keys = bits.ToArray();
            for (int i = 1; i < keys.Length; i++)
            {
                var key = keys[i];
                var at = idx[i];
                int j = i - 1;
                // Strictly greater only: equal keys keep their input order.
                for (; j >= 0 && keys[j] > key; j--)
                {
                    keys[j + 1] = keys[j];
                    idx[j + 1] = idx[j];
                }
                keys[j + 1] = key;
                idx[j + 1] = at;
            }
            return idx;
        }

        // LSD, 11-bit digits: six passes cover 64 bits. A pass where every key
        // has the same digit changes nothing and is skipped.
        private static int[] Radix(List<int> order, List<ulong> bits)
        {
            const int Width = 11, Buckets = 1 << Width, Mask = Buckets - 1;

            int n = order.Count;
            var keys = bits.ToArray();
            var idx = order.ToArray();
            var keysOut = new ulong[n];
            var idxOut = new int[n];
            var counts = new int[Buckets];

            for (int shift = 0; shift < 64; shift += Width)
            {
                Array.Clear(counts);
                foreach (var key in keys)
                    counts[(int)((key >> shift) & Mask)]++;
                if (counts[(int)((keys[0] >> shift) & Mask)] == n)
                    continue;

                for (int d = 0, sum = 0; d < Buckets; d++)
                {
                    int c = counts[d];
                    counts[d] = sum;
                    sum += c;
                }
                for (int i = 0; i < n; i++)
                {
                    int pos = counts[(int)((keys[i] >> shift) & Mask)]++;
                    keysOut[pos] = keys[i];
                    idxOut[pos] = idx[i];
                }
                (keys, keysOut) = (keysOut, keys);
                (idx, idxOut) = (idxOut, idx);
            }

            return idx;
        }

        private readonly record struct Entry(string Key, int Index);

        /// <summary>
        /// TimSort (Peters; the stack rule as fixed after de Gouw et al. 2015):
        /// natural runs, descending ones reversed, short ones extended to
        /// minRun by insertion; runs merged so the stack stays balanced; a merge
        /// first skips what is already in place, then gallops (binary search)
        /// once one side keeps winning. Equal keys never pass each other.
        /// </summary>
        private sealed class TimSort(Entry[] a, bool descending)
        {
            private const int MinGallop = 7;

            private readonly Entry[] _buffer = new Entry[a.Length];
            private readonly List<(int Start, int Length)> _runs = [];
            private int _minGallop = MinGallop;

            private bool Less(in Entry x, in Entry y)
            {
                int c = string.CompareOrdinal(x.Key, y.Key);
                return descending ? c > 0 : c < 0;
            }

            public void Sort()
            {
                int n = a.Length;
                if (n < 2)
                    return;
                if (n < Small)
                {
                    Insertion(0, n - 1, CountRun(0));
                    return;
                }

                int minRun = MinRun(n);
                for (int start = 0; start < n;)
                {
                    int run = CountRun(start);
                    if (run < minRun)
                    {
                        int forced = Math.Min(minRun, n - start);
                        Insertion(start, start + forced - 1, run);
                        run = forced;
                    }
                    _runs.Add((start, run));
                    Collapse(force: false);
                    start += run;
                }
                Collapse(force: true);
            }

            // 32..64 such that n / minRun is a power of two or just below one.
            private static int MinRun(int n)
            {
                int extra = 0;
                while (n >= 2 * Small)
                {
                    extra |= n & 1;
                    n >>= 1;
                }
                return n + extra;
            }

            // The run starting here; a strictly descending one is reversed (strictly, so equal keys keep their order).
            private int CountRun(int start)
            {
                int end = start + 1;
                if (end == a.Length)
                    return 1;
                if (Less(a[end++], a[start]))
                {
                    while (end < a.Length && Less(a[end], a[end - 1]))
                        end++;
                    Array.Reverse(a, start, end - start);
                }
                else
                {
                    while (end < a.Length && !Less(a[end], a[end - 1]))
                        end++;
                }
                return end - start;
            }

            // a[left..right] sorted, its first `sorted` elements already in order.
            private void Insertion(int left, int right, int sorted)
            {
                for (int i = left + Math.Max(sorted, 1); i <= right; i++)
                {
                    var x = a[i];
                    int j = i - 1;
                    for (; j >= left && Less(x, a[j]); j--)
                        a[j + 1] = a[j];
                    a[j + 1] = x;
                }
            }

            private void Collapse(bool force)
            {
                var runs = _runs;
                while (runs.Count > 1)
                {
                    int i = runs.Count - 2;
                    if (force)
                    {
                        if (i > 0 && runs[i - 1].Length < runs[i + 1].Length)
                            i--;
                    }
                    else if ((i > 0 && runs[i - 1].Length <= runs[i].Length + runs[i + 1].Length)
                             || (i > 1 && runs[i - 2].Length <= runs[i - 1].Length + runs[i].Length))
                    {
                        if (runs[i - 1].Length < runs[i + 1].Length)
                            i--;
                    }
                    else if (runs[i].Length > runs[i + 1].Length)
                    {
                        break;
                    }

                    var (start, length) = runs[i];
                    var next = runs[i + 1];
                    Merge(start, length, next.Start, next.Length);
                    runs[i] = (start, length + next.Length);
                    runs.RemoveAt(i + 1);
                }
            }

            // Two adjacent runs, the first copied aside and merged back from the left.
            private void Merge(int start1, int length1, int start2, int length2)
            {
                // Run 1's head that is <= run 2's first, and run 2's tail that is >= run 1's last, are in place.
                int skip = CountNotGreater(a, a[start2], start1, length1);
                start1 += skip;
                length1 -= skip;
                if (length1 == 0)
                    return;
                length2 = CountLess(a, a[start1 + length1 - 1], start2, length2);
                if (length2 == 0)
                    return;

                var buffer = _buffer;
                Array.Copy(a, start1, buffer, start1, length1);
                int i = start1, iEnd = start1 + length1, j = start2, jEnd = start2 + length2, o = start1;

                while (i < iEnd && j < jEnd)
                {
                    // One at a time until a side wins _minGallop times in a row.
                    int winsBuffer = 0, winsRun = 0;
                    while (i < iEnd && j < jEnd && (winsBuffer | winsRun) < _minGallop)
                    {
                        if (Less(a[j], buffer[i]))
                        {
                            a[o++] = a[j++];
                            winsRun++;
                            winsBuffer = 0;
                        }
                        else
                        {
                            a[o++] = buffer[i++];
                            winsBuffer++;
                            winsRun = 0;
                        }
                    }

                    // Then in blocks found by binary search, while blocks stay long.
                    while (i < iEnd && j < jEnd)
                    {
                        int fromBuffer = CountNotGreater(buffer, a[j], i, iEnd - i);
                        Array.Copy(buffer, i, a, o, fromBuffer);
                        o += fromBuffer;
                        i += fromBuffer;
                        if (i == iEnd)
                            break;
                        a[o++] = a[j++];
                        if (j == jEnd)
                            break;

                        int fromRun = CountLess(a, buffer[i], j, jEnd - j);
                        Array.Copy(a, j, a, o, fromRun);
                        o += fromRun;
                        j += fromRun;
                        if (j == jEnd)
                            break;
                        a[o++] = buffer[i++];

                        if (fromBuffer < MinGallop && fromRun < MinGallop)
                        {
                            _minGallop++;
                            break;
                        }
                        if (_minGallop > 1)
                            _minGallop--;
                    }
                }

                Array.Copy(buffer, i, a, o, iEnd - i);
            }

            // How many of xs[start..start+length) are <= key.
            private int CountNotGreater(Entry[] xs, in Entry key, int start, int length)
            {
                int lo = 0, hi = length;
                while (lo < hi)
                {
                    int mid = (lo + hi) >>> 1;
                    if (Less(key, xs[start + mid]))
                        hi = mid;
                    else
                        lo = mid + 1;
                }
                return lo;
            }

            // How many of xs[start..start+length) are < key.
            private int CountLess(Entry[] xs, in Entry key, int start, int length)
            {
                int lo = 0, hi = length;
                while (lo < hi)
                {
                    int mid = (lo + hi) >>> 1;
                    if (Less(xs[start + mid], key))
                        lo = mid + 1;
                    else
                        hi = mid;
                }
                return lo;
            }
        }
    }
}
