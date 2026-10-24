// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Engine
{
    // Statistics, as Python's statistics module has them: variance and stdev
    // of a sample (n - 1), pvariance and pstdev of a whole population (n).
    // Each takes an optional lambda for the value: median(users, u => u.age).
    // An empty list (or one value, for a sample's spread) gives null.
    internal static partial class Functions
    {
        private static readonly Dictionary<string, Definition> Statistics = new(StringComparer.OrdinalIgnoreCase)
        {
            // The middle value; for an even count the mean of the two middle ones.
            ["median"] = new(1, 2, a => Numbers(a, 1, "median") is { Length: > 0 } xs ? Quantile(xs, 0.5) : null),
            // percentile(list, p, fn?): p from 0 to 100, linearly interpolated (numpy's default).
            ["percentile"] = new(2, 3, a =>
            {
                var p = Values.ToNumber(a[1]);
                if (!(p >= 0 && p <= 100))
                    throw new InvalidOperationException($"'percentile' takes p from 0 to 100, not {Values.Format(p)}.");
                return Numbers(a, 2, "percentile") is { Length: > 0 } xs ? Quantile(xs, p / 100) : null;
            }),
            // The most common value (the first to reach the count, on a tie); any values, compared like ==.
            ["mode"] = new(1, 2, a =>
            {
                var (xs, fn) = (ListOf(a, "mode"), OptionalFn(a, 1, "mode"));
                // In order of first appearance (a dictionary only added to keeps it).
                var counts = new Dictionary<string, (object? Value, int Count)>(StringComparer.Ordinal);
                for (int i = 0; i < xs.Count; i++)
                {
                    Budget.Spend(1);
                    var value = fn is null ? xs[i] : fn.Invoke(xs[i], i);
                    var key = Values.Format(value);
                    counts[key] = counts.TryGetValue(key, out var seen) ? (seen.Value, seen.Count + 1) : (value, 1);
                }

                // Strictly more: on a tie the value seen first in the data wins, as in Python.
                object? best = null;
                int bestCount = 0;
                foreach (var (value, count) in counts.Values)
                    if (count > bestCount)
                        (best, bestCount) = (value, count);
                return best;
            }),
            ["variance"] = new(1, 2, a => Spread(Numbers(a, 1, "variance"), sample: true)),
            ["stdev"] = new(1, 2, a => Spread(Numbers(a, 1, "stdev"), sample: true) is double v ? Math.Sqrt(v) : null),
            ["pvariance"] = new(1, 2, a => Spread(Numbers(a, 1, "pvariance"), sample: false)),
            ["pstdev"] = new(1, 2, a => Spread(Numbers(a, 1, "pstdev"), sample: false) is double v ? Math.Sqrt(v) : null),
        };

        // The list's numbers, through the lambda at index when there is one.
        private static double[] Numbers(object?[] arguments, int lambda, string name)
        {
            var items = ListOf(arguments, name);
            var fn = arguments.Length > lambda ? Fn(arguments, lambda, name) : null;
            var numbers = new double[items.Count];
            for (int i = 0; i < numbers.Length; i++)
            {
                Budget.Spend(1);
                numbers[i] = fn is null ? Values.ToNumber(items[i]) : fn.InvokeNumber(items[i], i);
            }
            return numbers;
        }

        // Welford's one-pass mean and sum of squares: no cancellation on large values.
        private static double? Spread(double[] xs, bool sample)
        {
            if (xs.Length < (sample ? 2 : 1))
                return null;
            double mean = 0, squares = 0;
            for (int i = 0; i < xs.Length; i++)
            {
                double delta = xs[i] - mean;
                mean += delta / (i + 1);
                squares += delta * (xs[i] - mean);
            }
            return squares / (sample ? xs.Length - 1 : xs.Length);
        }

        // The q-quantile by linear interpolation between the order statistics
        // around q (n - 1), found by quickselect: O(n), not a full sort.
        private static double Quantile(double[] xs, double q)
        {
            if (xs.Any(double.IsNaN))
                return double.NaN;
            double position = q * (xs.Length - 1);
            int lower = (int)Math.Floor(position);
            double low = Select(xs, lower);
            if (position == lower)
                return low;
            // Everything right of the lower statistic is >= it: the next one is their minimum.
            double high = double.PositiveInfinity;
            for (int i = lower + 1; i < xs.Length; i++)
                high = Math.Min(high, xs[i]);
            return low + (high - low) * (position - lower);
        }

        // Hoare's quickselect with a random pivot (so no input is slow on
        // purpose): leaves xs[k] in its sorted place, smaller ones left, larger right.
        private static double Select(double[] xs, int k)
        {
            int left = 0, right = xs.Length - 1;
            while (left < right)
            {
                double pivot = xs[Random.Shared.Next(left, right + 1)];
                int i = left, j = right;
                while (i <= j)
                {
                    while (xs[i] < pivot)
                        i++;
                    while (xs[j] > pivot)
                        j--;
                    if (i <= j)
                    {
                        (xs[i], xs[j]) = (xs[j], xs[i]);
                        i++;
                        j--;
                    }
                }
                if (k <= j)
                    right = j;
                else if (k >= i)
                    left = i;
                else
                    return xs[k];
            }
            return xs[k];
        }
    }
}
