// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Storage
{
    /// <summary>
    /// Files kept by key in object storage (an S3 bucket, or memory in tests).
    /// Keys are paths such as <c>users/42/avatar.png</c>; there are no folders,
    /// only shared prefixes.
    /// </summary>
    public interface IObjectStorage
    {
        /// <summary>Whether object storage is set up (<c>Storage:S3</c>).</summary>
        bool IsConfigured { get; }

        /// <summary>Reaches the bucket; throws with the reason when it cannot.</summary>
        Task CheckAsync(CancellationToken cancellationToken);

        /// <summary>Stores <paramref name="content"/> under the key, replacing what was there.</summary>
        Task PutAsync(string key, Stream content, string? contentType, CancellationToken cancellationToken);

        /// <summary>
        /// The object and its content (dispose it), or null when there is none;
        /// with <paramref name="range"/>, only those bytes of the content.
        /// </summary>
        Task<StoredObject?> GetAsync(string key, CancellationToken cancellationToken, ByteSpan? range = null);

        /// <summary>What is stored under the key, without its content, or null.</summary>
        Task<ObjectInfo?> HeadAsync(string key, CancellationToken cancellationToken);

        /// <summary>Removes the object; removing one that is not there is not an error.</summary>
        Task DeleteAsync(string key, CancellationToken cancellationToken);

        /// <summary>The objects whose keys start with <paramref name="prefix"/>, in key order.</summary>
        IAsyncEnumerable<ObjectInfo> ListAsync(string? prefix, CancellationToken cancellationToken);
    }

    /// <summary>Bytes <paramref name="From"/> to <paramref name="To"/> of an object, both included.</summary>
    public readonly record struct ByteSpan(long From, long To)
    {
        public long Length => To - From + 1;
    }

    /// <summary>An object's key, size in bytes, last change and content type.</summary>
    public sealed record ObjectInfo(string Key, long Size, DateTimeOffset? LastModified, string? ContentType);

    /// <summary>An object with its content; the content is read once, then disposed.</summary>
    public sealed class StoredObject : IAsyncDisposable, IDisposable
    {
        public StoredObject(ObjectInfo info, Stream content)
        {
            Info = info;
            Content = content;
        }

        public ObjectInfo Info { get; }
        public Stream Content { get; }

        public ValueTask DisposeAsync() => Content.DisposeAsync();
        public void Dispose() => Content.Dispose();
    }

    /// <summary>Object storage is used but <c>Storage:S3</c> (or <c>S3_ENDPOINT</c>) is not set.</summary>
    public sealed class ObjectStorageNotConfiguredException : InvalidOperationException
    {
        public ObjectStorageNotConfiguredException()
            : base("Object storage is not configured: set Storage:S3 (or S3_ENDPOINT and the other S3_* variables).")
        {
        }
    }
}
