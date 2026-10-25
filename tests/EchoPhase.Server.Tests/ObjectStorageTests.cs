// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using System.Text;
using Amazon.S3;
using EchoPhase.Configuration.Storage.S3;
using EchoPhase.Storage;
using Microsoft.Extensions.Options;

namespace EchoPhase.Server.Tests
{
    /// <summary>
    /// What every <see cref="IObjectStorage"/> must do; each implementation
    /// runs the same tests, under a prefix of its own so runs do not meet.
    /// </summary>
    public abstract class ObjectStorageContract
    {
        protected abstract IObjectStorage Storage { get; }

        protected string Prefix { get; } = $"tests/{Guid.NewGuid():N}/";

        private static MemoryStream Text(string value) => new(Encoding.UTF8.GetBytes(value));

        private async Task<string> ReadAsync(string key)
        {
            await using var stored = await Storage.GetAsync(key, CancellationToken.None);
            Assert.NotNull(stored);
            using var reader = new StreamReader(stored.Content);
            return await reader.ReadToEndAsync();
        }

        [Fact]
        public async Task Put_ThenGet_ReturnsTheContentAndItsSize()
        {
            var key = Prefix + "hello.txt";

            await Storage.PutAsync(key, Text("hello, world"), "text/plain", CancellationToken.None);

            Assert.Equal("hello, world", await ReadAsync(key));
            var head = await Storage.HeadAsync(key, CancellationToken.None);
            Assert.NotNull(head);
            Assert.Equal(12, head.Size);
            Assert.Equal("text/plain", head.ContentType);
        }

        [Fact]
        public async Task Put_AStreamWithoutALength_IsStored()
        {
            var key = Prefix + "stream.bin";
            var bytes = new byte[100_000];
            Random.Shared.NextBytes(bytes);

            await Storage.PutAsync(key, new ForwardOnlyStream(bytes), null, CancellationToken.None);

            Assert.Equal(bytes.LongLength, (await Storage.HeadAsync(key, CancellationToken.None))?.Size);
        }

        [Fact]
        public async Task Put_TheSameKeyAgain_ReplacesTheContent()
        {
            var key = Prefix + "replaced.txt";

            await Storage.PutAsync(key, Text("first"), null, CancellationToken.None);
            await Storage.PutAsync(key, Text("second"), null, CancellationToken.None);

            Assert.Equal("second", await ReadAsync(key));
        }

        [Fact]
        public async Task Get_AKeyThatIsNotThere_IsNull()
        {
            Assert.Null(await Storage.GetAsync(Prefix + "missing", CancellationToken.None));
            Assert.Null(await Storage.HeadAsync(Prefix + "missing", CancellationToken.None));
        }

        [Fact]
        public async Task List_ReturnsTheKeysUnderThePrefixInOrder()
        {
            await Storage.PutAsync(Prefix + "b/2.txt", Text("2"), null, CancellationToken.None);
            await Storage.PutAsync(Prefix + "a/1.txt", Text("1"), null, CancellationToken.None);
            await Storage.PutAsync(Prefix + "c.txt", Text("3"), null, CancellationToken.None);

            var keys = new List<string>();
            await foreach (var item in Storage.ListAsync(Prefix, CancellationToken.None))
                keys.Add(item.Key);
            var underA = new List<string>();
            await foreach (var item in Storage.ListAsync(Prefix + "a/", CancellationToken.None))
                underA.Add(item.Key);

            Assert.Equal([Prefix + "a/1.txt", Prefix + "b/2.txt", Prefix + "c.txt"], keys);
            Assert.Equal([Prefix + "a/1.txt"], underA);
        }

        [Fact]
        public async Task Delete_RemovesTheObject_AndAMissingOneIsNoError()
        {
            var key = Prefix + "gone.txt";
            await Storage.PutAsync(key, Text("bye"), null, CancellationToken.None);

            await Storage.DeleteAsync(key, CancellationToken.None);
            await Storage.DeleteAsync(key, CancellationToken.None);

            Assert.Null(await Storage.HeadAsync(key, CancellationToken.None));
        }

        [Fact]
        public async Task Check_ReachesTheStorage()
        {
            await Storage.CheckAsync(CancellationToken.None);
        }

        /// <summary>A stream that cannot seek or tell its length, like stdin.</summary>
        private sealed class ForwardOnlyStream : Stream
        {
            private readonly MemoryStream _inner;

            public ForwardOnlyStream(byte[] bytes)
            {
                _inner = new MemoryStream(bytes, writable: false);
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }

    public class InMemoryObjectStorageTests : ObjectStorageContract
    {
        protected override IObjectStorage Storage { get; } = new InMemoryObjectStorage();
    }

    /// <summary>
    /// The same tests against a real S3-compatible service. Only with
    /// ECHOPHASE_TEST_S3_ENDPOINT set (and ECHOPHASE_TEST_S3_ACCESS_KEY_ID,
    /// ECHOPHASE_TEST_S3_SECRET_ACCESS_KEY, optionally ECHOPHASE_TEST_S3_BUCKET);
    /// the bucket is created when missing, the test objects removed after.
    /// </summary>
    public sealed class S3ObjectStorageTests : ObjectStorageContract, IAsyncLifetime
    {
        private static readonly string? Endpoint = Environment.GetEnvironmentVariable("ECHOPHASE_TEST_S3_ENDPOINT");

        private readonly S3Options _options = new()
        {
            Endpoint = Endpoint ?? "http://localhost:8333",
            Bucket = Environment.GetEnvironmentVariable("ECHOPHASE_TEST_S3_BUCKET") ?? "echophase-tests",
            AccessKeyId = Environment.GetEnvironmentVariable("ECHOPHASE_TEST_S3_ACCESS_KEY_ID") ?? string.Empty,
            SecretAccessKey = Environment.GetEnvironmentVariable("ECHOPHASE_TEST_S3_SECRET_ACCESS_KEY") ?? string.Empty,
        };

        private readonly S3ObjectStorage _storage;

        public S3ObjectStorageTests()
        {
            _storage = new S3ObjectStorage(Options.Create(_options));
        }

        protected override IObjectStorage Storage => _storage;

        public async ValueTask InitializeAsync()
        {
            Assert.SkipWhen(Endpoint is null, "ECHOPHASE_TEST_S3_ENDPOINT is not set.");
            using var client = S3ObjectStorage.CreateClient(_options);
            try
            {
                await client.PutBucketAsync(_options.Bucket);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Already there.
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Endpoint is not null)
            {
                await foreach (var item in _storage.ListAsync(Prefix, CancellationToken.None))
                    await _storage.DeleteAsync(item.Key, CancellationToken.None);
            }
            _storage.Dispose();
        }
    }
}
