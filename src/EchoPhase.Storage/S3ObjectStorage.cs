// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using System.Runtime.CompilerServices;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EchoPhase.Configuration.Storage.S3;
using Microsoft.Extensions.Options;

namespace EchoPhase.Storage
{
    /// <summary>
    /// <see cref="IObjectStorage"/> on any S3-compatible service, in the bucket
    /// of <see cref="S3Options"/>.
    /// </summary>
    public sealed class S3ObjectStorage : IObjectStorage, IDisposable
    {
        private readonly S3Options _options;
        private readonly Lazy<IAmazonS3> _client;

        public S3ObjectStorage(IOptions<S3Options> options)
        {
            _options = options.Value;
            _client = new Lazy<IAmazonS3>(() => CreateClient(_options));
        }

        public bool IsConfigured => _options.IsConfigured;

        private IAmazonS3 Client => _options.IsConfigured ? _client.Value : throw new ObjectStorageNotConfiguredException();

        /// <summary>The client for these options; also used by tests against a real S3.</summary>
        public static IAmazonS3 CreateClient(S3Options options)
        {
            var config = new AmazonS3Config
            {
                ServiceURL = options.Endpoint,
                AuthenticationRegion = options.Region,
                ForcePathStyle = options.ForcePathStyle,
                // The SDK's default checksums (CRC32 headers and trailers) are
                // not understood by every S3-compatible server; send them only
                // where S3 requires them.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };
            AWSCredentials credentials = string.IsNullOrWhiteSpace(options.AccessKeyId)
                ? new AnonymousAWSCredentials()
                : new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey);
            return new AmazonS3Client(credentials, config);
        }

        public async Task CheckAsync(CancellationToken cancellationToken)
        {
            // Listing one key needs only read access, on every S3 flavour.
            await Client.ListObjectsV2Async(
                new ListObjectsV2Request { BucketName = _options.Bucket, MaxKeys = 1 }, cancellationToken);
        }

        public async Task PutAsync(string key, Stream content, string? contentType, CancellationToken cancellationToken)
        {
            // The request needs the length up front: buffer a stream that has
            // none (stdin, a network body) in a temporary file.
            await using var buffered = content.CanSeek ? null : await BufferAsync(content, cancellationToken);
            var request = new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                InputStream = buffered ?? content,
                AutoCloseStream = false,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            };
            await Client.PutObjectAsync(request, cancellationToken);
        }

        public async Task<StoredObject?> GetAsync(string key, CancellationToken cancellationToken)
        {
            try
            {
                var response = await Client.GetObjectAsync(_options.Bucket, key, cancellationToken);
                var info = new ObjectInfo(key, response.ContentLength, ToOffset(response.LastModified), response.Headers.ContentType);
                return new StoredObject(info, response.ResponseStream);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<ObjectInfo?> HeadAsync(string key, CancellationToken cancellationToken)
        {
            try
            {
                var response = await Client.GetObjectMetadataAsync(_options.Bucket, key, cancellationToken);
                return new ObjectInfo(key, response.ContentLength, ToOffset(response.LastModified), response.Headers.ContentType);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            await Client.DeleteObjectAsync(_options.Bucket, key, cancellationToken);
        }

        public async IAsyncEnumerable<ObjectInfo> ListAsync(string? prefix, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var request = new ListObjectsV2Request { BucketName = _options.Bucket, Prefix = prefix };
            bool more;
            do
            {
                var response = await Client.ListObjectsV2Async(request, cancellationToken);
                foreach (var item in response.S3Objects ?? [])
                    yield return new ObjectInfo(item.Key, item.Size ?? 0, ToOffset(item.LastModified), null);
                request.ContinuationToken = response.NextContinuationToken;
                more = response.IsTruncated == true;
            }
            while (more);
        }

        public void Dispose()
        {
            if (_client.IsValueCreated)
                _client.Value.Dispose();
        }

        private static DateTimeOffset? ToOffset(DateTime? value) =>
            value is { } time ? new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc)) : null;

        private static async Task<Stream> BufferAsync(Stream content, CancellationToken cancellationToken)
        {
            var file = new FileStream(Path.GetTempFileName(), FileMode.Open, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
            await content.CopyToAsync(file, cancellationToken);
            file.Position = 0;
            return file;
        }
    }
}
