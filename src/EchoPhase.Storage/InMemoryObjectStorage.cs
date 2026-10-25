// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace EchoPhase.Storage
{
    /// <summary>
    /// <see cref="IObjectStorage"/> in memory: for tests, and for code that
    /// needs storage without an S3 at hand.
    /// </summary>
    public sealed class InMemoryObjectStorage : IObjectStorage
    {
        private readonly ConcurrentDictionary<string, (byte[] Content, string? ContentType, DateTimeOffset Modified)> _objects =
            new(StringComparer.Ordinal);
        private readonly TimeProvider _time;

        public InMemoryObjectStorage(TimeProvider? time = null)
        {
            _time = time ?? TimeProvider.System;
        }

        public bool IsConfigured => true;

        public Task CheckAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task PutAsync(string key, Stream content, string? contentType, CancellationToken cancellationToken)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            _objects[key] = (copy.ToArray(), contentType, _time.GetUtcNow());
        }

        public Task<StoredObject?> GetAsync(string key, CancellationToken cancellationToken, ByteSpan? range = null)
        {
            if (!_objects.TryGetValue(key, out var item))
                return Task.FromResult<StoredObject?>(null);
            var content = range is { } span
                ? new MemoryStream(item.Content, (int)span.From, (int)span.Length, writable: false)
                : new MemoryStream(item.Content, writable: false);
            return Task.FromResult<StoredObject?>(new StoredObject(Info(key, item), content));
        }

        public Task<ObjectInfo?> HeadAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(_objects.TryGetValue(key, out var item) ? Info(key, item) : null);

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            _objects.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<ObjectInfo> ListAsync(string? prefix, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var (key, item) in _objects.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (prefix is null || key.StartsWith(prefix, StringComparison.Ordinal))
                    yield return Info(key, item);
            }
            await Task.CompletedTask;
        }

        private static ObjectInfo Info(string key, (byte[] Content, string? ContentType, DateTimeOffset Modified) item) =>
            new(key, item.Content.LongLength, item.Modified, item.ContentType);
    }
}
