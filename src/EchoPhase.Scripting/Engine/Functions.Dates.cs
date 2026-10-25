// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EchoPhase.Scripting.Engine
{
    // Dates are ISO 8601 text with an offset ("2026-10-20T09:30:00+03:00"), so
    // they print, compare and store as is. A time zone is an IANA id; without
    // one, the program's (settings.timeZone), else UTC. A date without an
    // offset is read in that zone.
    internal static partial class Functions
    {
        // No zone argument: the program's zone.
        private const int ProgramZone = int.MaxValue;

        private const string IsoFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz";

        // An offset after a time ("T09:00Z", "09:00:00+03:00"), not the day of "2026-10-20".
        private static readonly Regex HasOffset = new(@"\d{2}:\d{2}(:\d{2}(\.\d+)?)?\s*(Z|[+-]\d{2}(:?\d{2})?)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly ConcurrentDictionary<string, TimeZoneInfo> Zones = new(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Definition> Dates = new(StringComparer.OrdinalIgnoreCase)
        {
            ["now"] = new(0, 1, (a, v) => Iso(DateTimeOffset.UtcNow, Zone(a, 0, v))),
            ["today"] = new(0, 1, (a, v) => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone(a, 0, v)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            // date('2026-10-20 09:30') -> ISO in the zone
            ["date"] = new(1, 2, (a, v) => Iso(ToDate(a[0], Zone(a, 1, v)), Zone(a, 1, v))),
            // formatDate(d, 'dd.MM.yyyy HH:mm', tz?): .NET custom format, invariant culture.
            ["formatDate"] = new(2, 3, (a, v) => TimeZoneInfo.ConvertTime(ToDate(a[0], Zone(a, 2, v)), Zone(a, 2, v))
                .ToString(Values.Format(a[1]), CultureInfo.InvariantCulture)),
            // dateAdd(d, 3, 'days'); days and longer follow the zone's calendar (DST included).
            ["dateAdd"] = new(3, 3, (a, v) => Add(ToDate(a[0], Zone(a, ProgramZone, v)), Values.ToNumber(a[1]), Values.Format(a[2]), Zone(a, ProgramZone, v))),
            // dateDiff(a, b, 'hours') = a - b; months and years count whole ones.
            ["dateDiff"] = new(3, 3, (a, v) => Diff(ToDate(a[0], Zone(a, ProgramZone, v)), ToDate(a[1], Zone(a, ProgramZone, v)), Values.Format(a[2]), Zone(a, ProgramZone, v))),
            // datePart(d, 'weekday') -> 1 (Monday) .. 7
            ["datePart"] = new(2, 3, (a, v) => Part(TimeZoneInfo.ConvertTime(ToDate(a[0], Zone(a, 2, v)), Zone(a, 2, v)), Values.Format(a[1]))),
            ["startOf"] = new(2, 3, (a, v) => StartOf(ToDate(a[0], Zone(a, 2, v)), Values.Format(a[1]), Zone(a, 2, v))),
            ["toUnix"] = new(1, 1, (a, v) => (double)ToDate(a[0], Zone(a, ProgramZone, v)).ToUnixTimeSeconds()),
            ["fromUnix"] = new(1, 2, (a, v) => Iso(DateTimeOffset.FromUnixTimeMilliseconds((long)(Values.ToNumber(a[0]) * 1000)), Zone(a, 1, v))),
        };

        // The zone argument at index, else the program's, else UTC.
        private static TimeZoneInfo Zone(object?[] arguments, int index, IDictionary<string, object?> variables)
        {
            var id = arguments.Length > index && Values.Unwrap(arguments[index]) is { } given
                ? Values.Format(given)
                : variables.TryGetValue(Eval.TimeZoneVariable, out var program) && program is string s && s.Length > 0
                    ? s
                    : "UTC";

            return Zones.GetOrAdd(id, static zone =>
                TimeZoneInfo.TryFindSystemTimeZoneById(zone, out var found)
                    ? found
                    : throw new InvalidOperationException($"'{zone}' is not a time zone, e.g. 'Europe/Moscow'."));
        }

        private static string Iso(DateTimeOffset value, TimeZoneInfo zone)
            => TimeZoneInfo.ConvertTime(value, zone).ToString(IsoFormat, CultureInfo.InvariantCulture);

        private static DateTimeOffset ToDate(object? value, TimeZoneInfo zone)
        {
            switch (Values.Unwrap(value))
            {
                case DateTimeOffset dto:
                    return dto;
                case DateTime { Kind: DateTimeKind.Unspecified } local:
                    return new DateTimeOffset(local, zone.GetUtcOffset(local));
                case DateTime dt:
                    return new DateTimeOffset(dt.ToUniversalTime());
                case string s when HasOffset.IsMatch(s.Trim())
                                   && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed):
                    return parsed;
                case string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var local):
                    local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
                    return new DateTimeOffset(local, zone.GetUtcOffset(local));
                case var v when Values.IsNumber(v):
                    throw new InvalidOperationException($"{Values.Describe(v)} is a number, not a date; use fromUnix().");
                default:
                    throw new InvalidOperationException($"{Values.Describe(value)} is not a date.");
            }
        }

        // In the zone's local time, then back with that moment's offset.
        private static DateTimeOffset InZone(DateTime local, TimeZoneInfo zone)
        {
            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            // A time skipped by DST does not exist: take the next valid one.
            while (zone.IsInvalidTime(local))
                local = local.AddMinutes(30);
            return new DateTimeOffset(local, zone.GetUtcOffset(local));
        }

        private static string Add(DateTimeOffset date, double amount, string unit, TimeZoneInfo zone)
        {
            var local = TimeZoneInfo.ConvertTime(date, zone).DateTime;
            var result = Unit(unit) switch
            {
                "millisecond" => date.AddMilliseconds(amount),
                "second" => date.AddSeconds(amount),
                "minute" => date.AddMinutes(amount),
                "hour" => date.AddHours(amount),
                "day" => InZone(local.AddDays(amount), zone),
                "week" => InZone(local.AddDays(amount * 7), zone),
                "month" => InZone(local.AddMonths(WholeNumber(amount, unit)), zone),
                "year" => InZone(local.AddYears(WholeNumber(amount, unit)), zone),
                var other => throw UnknownUnit(other)
            };
            return Iso(result, zone);
        }

        private static double Diff(DateTimeOffset left, DateTimeOffset right, string unit, TimeZoneInfo zone)
        {
            var span = left - right;
            return Unit(unit) switch
            {
                "millisecond" => span.TotalMilliseconds,
                "second" => span.TotalSeconds,
                "minute" => span.TotalMinutes,
                "hour" => span.TotalHours,
                "day" => span.TotalDays,
                "week" => span.TotalDays / 7,
                "month" => Months(left, right, zone),
                "year" => Math.Truncate(Months(left, right, zone) / 12),
                var other => throw UnknownUnit(other)
            };
        }

        // Whole calendar months from right to left (negative when left is earlier).
        private static double Months(DateTimeOffset left, DateTimeOffset right, TimeZoneInfo zone)
        {
            var l = TimeZoneInfo.ConvertTime(left, zone).DateTime;
            var r = TimeZoneInfo.ConvertTime(right, zone).DateTime;
            int months = (l.Year - r.Year) * 12 + l.Month - r.Month;
            if (months > 0 && r.AddMonths(months) > l)
                months--;
            else if (months < 0 && r.AddMonths(months) < l)
                months++;
            return months;
        }

        private static object Part(DateTimeOffset local, string part) => part.ToLowerInvariant() switch
        {
            "year" => local.Year,
            "month" => local.Month,
            "day" => local.Day,
            "hour" => local.Hour,
            "minute" => local.Minute,
            "second" => local.Second,
            "millisecond" => local.Millisecond,
            "weekday" => local.DayOfWeek == DayOfWeek.Sunday ? 7d : (double)local.DayOfWeek,
            "dayofyear" => local.DayOfYear,
            "week" => ISOWeek.GetWeekOfYear(local.DateTime),
            "offset" => local.Offset.TotalMinutes,
            var other => throw new InvalidOperationException(
                $"Unknown date part '{other}'; use year, month, day, hour, minute, second, millisecond, weekday, dayOfYear, week or offset.")
        };

        private static string StartOf(DateTimeOffset date, string unit, TimeZoneInfo zone)
        {
            var l = TimeZoneInfo.ConvertTime(date, zone).DateTime;
            var start = Unit(unit) switch
            {
                "second" => new DateTime(l.Year, l.Month, l.Day, l.Hour, l.Minute, l.Second),
                "minute" => new DateTime(l.Year, l.Month, l.Day, l.Hour, l.Minute, 0),
                "hour" => new DateTime(l.Year, l.Month, l.Day, l.Hour, 0, 0),
                "day" => l.Date,
                "week" => l.Date.AddDays(-(((int)l.DayOfWeek + 6) % 7)),
                "month" => new DateTime(l.Year, l.Month, 1),
                "year" => new DateTime(l.Year, 1, 1),
                var other => throw UnknownUnit(other)
            };
            return Iso(InZone(start, zone), zone);
        }

        // 'days', 'Day', 'd' -> day
        private static string Unit(string unit) => unit.Trim().ToLowerInvariant() switch
        {
            "ms" or "millisecond" or "milliseconds" => "millisecond",
            "s" or "sec" or "second" or "seconds" => "second",
            "m" or "min" or "minute" or "minutes" => "minute",
            "h" or "hour" or "hours" => "hour",
            "d" or "day" or "days" => "day",
            "w" or "week" or "weeks" => "week",
            "month" or "months" => "month",
            "y" or "year" or "years" => "year",
            var other => other
        };

        private static int WholeNumber(double amount, string unit)
            => amount == Math.Floor(amount)
                ? (int)amount
                : throw new InvalidOperationException($"Add whole {unit}, not {Values.Format(amount)}.");

        private static InvalidOperationException UnknownUnit(string unit)
            => new($"Unknown unit '{unit}'; use milliseconds, seconds, minutes, hours, days, weeks, months or years.");
    }
}
