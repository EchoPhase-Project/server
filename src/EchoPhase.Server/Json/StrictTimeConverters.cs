// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EchoPhase.Server.Json
{
    /// <summary>
    /// Request times must say which moment they mean: ISO 8601 with "Z" or an
    /// offset. "2026-10-10T10:00" without one is rejected (400) rather than
    /// read in the server's time zone, which differs between the pod (UTC)
    /// and a developer machine. Writing is unchanged.
    /// </summary>
    internal static partial class StrictTime
    {
        public const string Expected = "an ISO 8601 time with Z or an offset, e.g. 2026-10-10T10:00:00+03:00";

        [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$")]
        private static partial Regex WithOffset();

        public static DateTimeOffset Read(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected {Expected}.");

            var text = reader.GetString()!;
            if (!WithOffset().IsMatch(text)
                || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
                throw new JsonException($"'{text}' is not {Expected}.");

            return value;
        }
    }

    public sealed class StrictDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => StrictTime.Read(ref reader);

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }

    /// <summary>
    /// Same rule for <see cref="DateTime"/> fields, read as UTC
    /// (<see cref="DateTimeKind.Utc"/>, what Npgsql stores), so a DTO that
    /// still uses DateTime cannot let a zone-less time in either.
    /// </summary>
    public sealed class StrictUtcDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => StrictTime.Read(ref reader).UtcDateTime;

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }
}
