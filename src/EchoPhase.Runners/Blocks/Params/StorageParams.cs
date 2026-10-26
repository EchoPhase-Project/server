// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using System.Text.Json.Serialization;
using EchoPhase.Configuration;
using EchoPhase.Runners.Blocks.Storage;
using EchoPhase.Types.Attributes;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    [ExtensibleEnum]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StorageOperation
    {
        /// <summary>Output: the value, or `default` when there is none.</summary>
        Get,

        /// <summary>Writes `value`; output: the value.</summary>
        Set,

        /// <summary>`value` is computed from the current one (the variable `value`), atomically; output: the new value.</summary>
        Update,

        /// <summary>Output: whether there was a value.</summary>
        Delete,

        /// <summary>Output: [{ key, size, updatedAt, expiresAt[, value] }], by key.</summary>
        List
    }

    public class StorageParams : IValidatable
    {
        public const int MaxListLimit = 1000;

        [JsonPropertyName("operation")]
        public StorageOperation Operation { get; set; } = StorageOperation.Get;

        /// <summary>A template: "seen", "user:@input.id".</summary>
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        /// <summary>Workflow: this workflow's own; Package: shared by the workflows of its package.</summary>
        [JsonPropertyName("scope")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StorageScope Scope { get; set; } = StorageScope.Workflow;

        /// <summary>
        /// Set: a template ("@input") or a JSON value. Update: the same, where
        /// `value` is the current value (null at first):
        /// "@(bloomAdd(coalesce(value, bloom(10000)), input.id))".
        /// </summary>
        [JsonPropertyName("value")]
        public JsonElement Value { get; set; }

        /// <summary>Get: the output when there is no value (a template or JSON value).</summary>
        [JsonPropertyName("default")]
        public JsonElement Default { get; set; }

        /// <summary>Set/Update: seconds until the value expires; absent: Set keeps it, Update keeps its expiry.</summary>
        [JsonPropertyName("ttlSeconds")]
        public int? TtlSeconds { get; set; }

        /// <summary>List: only keys starting with this (a template).</summary>
        [JsonPropertyName("prefix")]
        public string? Prefix { get; set; }

        /// <summary>List: the key to continue after (a template).</summary>
        [JsonPropertyName("after")]
        public string? After { get; set; }

        /// <summary>List: at most this many keys.</summary>
        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 100;

        /// <summary>List: include each value.</summary>
        [JsonPropertyName("values")]
        public bool Values { get; set; }

        public IValidationResult Validate()
        {
            if (Operation != StorageOperation.List && string.IsNullOrWhiteSpace(Key))
                return ValidationResult.Failure(error => error.Set(nameof(Key), "Key cannot be empty."));
            if (Operation is StorageOperation.Set or StorageOperation.Update && Value.ValueKind == JsonValueKind.Undefined)
                return ValidationResult.Failure(error => error.Set(nameof(Value), $"{Operation} needs a value."));
            if (TtlSeconds is <= 0)
                return ValidationResult.Failure(error => error.Set(nameof(TtlSeconds), "TtlSeconds must be positive."));
            if (Limit is < 1 or > MaxListLimit)
                return ValidationResult.Failure(error => error.Set(nameof(Limit), $"Limit must be between 1 and {MaxListLimit}."));
            return ValidationResult.Success();
        }
    }
}
