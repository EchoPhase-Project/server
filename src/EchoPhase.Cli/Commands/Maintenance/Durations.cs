// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Text.RegularExpressions;

namespace EchoPhase.Cli.Commands.Maintenance
{
    /// <summary>Durations as people type them: 90s, 30m, 2h, 1d, or hh:mm:ss.</summary>
    public static partial class Durations
    {
        [GeneratedRegex(@"^\s*(\d+)\s*([smhd])\s*$", RegexOptions.IgnoreCase)]
        private static partial Regex Short();

        public static bool TryParse(string? text, out TimeSpan duration)
        {
            duration = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var match = Short().Match(text);
            if (match.Success && long.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var amount))
            {
                try
                {
                    duration = char.ToLowerInvariant(match.Groups[2].Value[0]) switch
                    {
                        's' => TimeSpan.FromSeconds(amount),
                        'm' => TimeSpan.FromMinutes(amount),
                        'h' => TimeSpan.FromHours(amount),
                        _ => TimeSpan.FromDays(amount)
                    };
                }
                catch (OverflowException)
                {
                    return false;
                }
                return duration > TimeSpan.Zero;
            }

            return TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out duration) && duration > TimeSpan.Zero;
        }
    }
}
