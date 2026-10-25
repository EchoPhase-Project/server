// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.RegularExpressions;

namespace EchoPhase.Configuration.Storage.S3
{
    /// <summary>
    /// Object storage for files: any S3-compatible service (SeaweedFS, AWS S3,
    /// Cloudflare R2, Garage, Ceph RGW...). Optional: without an endpoint the
    /// server runs without object storage.
    /// </summary>
    public partial class S3Options : IValidatable
    {
        public const string SectionName = "Storage:S3";

        /// <summary>
        /// The service's URL, e.g. <c>https://s3.eu-central-1.amazonaws.com</c>
        /// or <c>http://seaweedfs:8333</c>. Empty: no object storage.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>The region requests are signed for; S3-compatible servers mostly accept any.</summary>
        public string Region { get; set; } = "us-east-1";

        /// <summary>The bucket the server keeps its files in.</summary>
        public string Bucket { get; set; } = string.Empty;

        public string AccessKeyId { get; set; } = string.Empty;
        public string SecretAccessKey { get; set; } = string.Empty;

        /// <summary>
        /// The bucket in the path (<c>https://host/bucket/key</c>) instead of the
        /// host name: what most self-hosted S3 servers need.
        /// </summary>
        public bool ForcePathStyle { get; set; } = true;

        /// <summary>Whether object storage is set up at all.</summary>
        public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);

        public IValidationResult Validate()
        {
            if (!IsConfigured)
                return ValidationResult.Success();

            if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Endpoint), "Endpoint must be an absolute http or https URL."));

            if (string.IsNullOrWhiteSpace(Region))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Region), "Region cannot be empty."));

            if (!BucketName().IsMatch(Bucket))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Bucket),
                        "Bucket must be 3 to 63 lowercase letters, digits, dots or hyphens, starting and ending with a letter or digit."));

            if (string.IsNullOrWhiteSpace(AccessKeyId) != string.IsNullOrWhiteSpace(SecretAccessKey))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(SecretAccessKey), "Access key id and secret access key go together."));

            return ValidationResult.Success();
        }

        // S3's bucket naming rules, as far as every S3-compatible service shares them.
        [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$")]
        private static partial Regex BucketName();
    }
}
