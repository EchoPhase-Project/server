// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Storage.S3
{
    /// <summary>
    /// <see cref="S3Options"/> from the <c>S3_*</c> environment variables that
    /// containers and the Helm chart set; each set variable wins over the
    /// configuration files.
    /// </summary>
    public static class S3Environment
    {
        /// <summary>
        /// Applies <c>S3_ENDPOINT</c>, <c>S3_REGION</c>, <c>S3_BUCKET</c>,
        /// <c>S3_ACCESS_KEY_ID</c>, <c>S3_SECRET_ACCESS_KEY</c> and
        /// <c>S3_FORCE_PATH_STYLE</c> (true or false) where they are set and
        /// not empty.
        /// </summary>
        /// <param name="options">The options to change.</param>
        /// <param name="variable">Reads an environment variable by name.</param>
        public static void Apply(S3Options options, Func<string, string?> variable)
        {
            if (Value(variable, "S3_ENDPOINT") is { } endpoint)
                options.Endpoint = endpoint;
            if (Value(variable, "S3_REGION") is { } region)
                options.Region = region;
            if (Value(variable, "S3_BUCKET") is { } bucket)
                options.Bucket = bucket;
            if (Value(variable, "S3_ACCESS_KEY_ID") is { } accessKeyId)
                options.AccessKeyId = accessKeyId;
            if (Value(variable, "S3_SECRET_ACCESS_KEY") is { } secretAccessKey)
                options.SecretAccessKey = secretAccessKey;
            if (bool.TryParse(Value(variable, "S3_FORCE_PATH_STYLE"), out var forcePathStyle))
                options.ForcePathStyle = forcePathStyle;
        }

        private static string? Value(Func<string, string?> variable, string name) =>
            variable(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
    }
}
