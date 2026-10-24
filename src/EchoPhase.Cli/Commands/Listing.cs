// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EchoPhase.Cli.Commands
{
    /// <summary>
    /// Options of every list: one page at a time (--limit, then --after with the
    /// cursor the previous page printed), or with --all and --json everything,
    /// streamed in batches without holding it in memory.
    /// </summary>
    public class ListingSettings : CommandSettings
    {
        public const int MaxLimit = 1000;

        [CommandOption("--limit|-n")]
        [DefaultValue(50)]
        [Description("Rows per page (1-1000)")]
        public int Limit { get; set; } = 50;

        [CommandOption("--after")]
        [Description("Continue after this cursor (printed under the previous page)")]
        public string? After { get; set; }

        [CommandOption("--all")]
        [Description("Every row, in batches; only with --json")]
        public bool All { get; set; }

        [CommandOption("--json")]
        [Description("One JSON object per line (NDJSON) instead of a table; the next cursor goes to stderr")]
        public bool Json { get; set; }

        public override ValidationResult Validate()
        {
            if (Limit is < 1 or > MaxLimit)
                return ValidationResult.Error($"--limit must be between 1 and {MaxLimit}.");
            if (All && !Json)
                return ValidationResult.Error("--all needs --json: a table of everything is not readable. Page with --after, or pipe --json --all.");
            if (All && After is not null)
                return ValidationResult.Error("--all starts from the beginning; drop --after.");
            return ValidationResult.Success();
        }
    }

    public static class Listing
    {
        /// <summary>Rows per query with --all.</summary>
        public const int BatchSize = 500;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        /// <summary>
        /// Fetches one page (or with --all, every batch) and hands each to
        /// <paramref name="page"/>. <paramref name="fetch"/> gets the cursor to
        /// continue after and how many rows to take; one more than shown is
        /// asked for to know whether more follow. Returns the cursor of the next
        /// page, or null when this was the last.
        /// </summary>
        public static async Task<string?> PagesAsync<T>(
            ListingSettings settings,
            Func<string?, int, CancellationToken, Task<List<T>>> fetch,
            Func<T, string> cursorOf,
            Func<IReadOnlyList<T>, Task> page,
            CancellationToken ct)
        {
            var size = settings.All ? BatchSize : settings.Limit;
            var after = settings.After;
            while (true)
            {
                var rows = await fetch(after, size + 1, ct);
                var more = rows.Count > size;
                if (more)
                    rows.RemoveAt(rows.Count - 1);

                if (rows.Count > 0)
                    await page(rows);

                after = more ? cursorOf(rows[^1]) : null;
                if (!settings.All || after is null)
                    return after;
            }
        }

        /// <summary>One NDJSON line on stdout: plain, without console markup or wrapping.</summary>
        public static void WriteJson<T>(T value)
            => Console.Out.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

        /// <summary>A JSON document on stdout, indented for reading.</summary>
        public static void WriteJsonDocument<T>(T value)
            => Console.Out.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonOptions) { WriteIndented = true }));

        /// <summary>
        /// After a page: how to get the next one. Under a table it says so; with
        /// --json it goes to stderr, so stdout stays data only.
        /// </summary>
        public static void Footer(ListingSettings settings, string? next)
        {
            if (next is null)
                return;

            if (settings.Json)
                Console.Error.WriteLine($"next: {next}");
            else
                AnsiConsole.MarkupLineInterpolated($"[grey]More: the same command with --after {next}[/]");
        }

        /// <summary>LIKE pattern matching the text anywhere, with its wildcards taken literally.</summary>
        public static string Contains(string text)
            => "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

        public static string Time(DateTime? utc) => utc is { } t ? t.ToString("u") : "";

        public static string Time(DateTimeOffset? at) => at is { } t ? t.UtcDateTime.ToString("u") : "";
    }
}
