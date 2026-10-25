// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Validation;
using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    [OpCodePayload(OpCodes.Handshake)]
    public class HandshakePayload : IPayload
    {
        /// <summary>Intent names to receive, e.g. ["blocks", "connections"]; omitted: keep the current ones.</summary>
        public string[]? Intents
        {
            get; set;
        }

        /// <summary>
        /// A shard pool to join (letters, digits, '.', '_', '-'; up to 64):
        /// each event goes to one connection of the pool, not to all. Omitted:
        /// shared, every event (or, on a second handshake, the pool stays).
        /// </summary>
        public string? ShardPool
        {
            get; set;
        }

        public const int MaxShardPoolLength = 64;

        public HandshakePayload()
        {
        }

        public IValidationResult Validate()
        {
            if (Intents is { } list && (list.Length > IntentLimits.MaxIntents || list.Any(n => n?.Length > IntentLimits.MaxNameLength)))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Intents), $"At most {IntentLimits.MaxIntents} intents of up to {IntentLimits.MaxNameLength} characters."));

            if (Intents is not null && Intents.Any(string.IsNullOrWhiteSpace))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Intents), "Intent names cannot be empty."));

            if (ShardPool is { } pool && (pool.Length is 0 or > MaxShardPoolLength
                || !pool.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(ShardPool),
                        $"A shard pool is 1-{MaxShardPoolLength} letters, digits, '.', '_' or '-'."));

            return ValidationResult.Success();
        }
    }
}
