// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using EchoPhase.Configuration.Storage.Files;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Files;
using EchoPhase.Server.Controllers.Api.v1.Files;
using EchoPhase.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace EchoPhase.Server.Tests
{
    /// <summary>FileService's rules (purposes, limits, quota, ownership, cleanup) on EF's in-memory provider and in-memory storage.</summary>
    public class FileServiceTests : IDisposable
    {
        private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
        private readonly PostgresContext _db;
        private readonly InMemoryObjectStorage _storage;
        private readonly FilesOptions _options = new() { AvatarMaxBytes = 64, MaxFileBytes = 1000, QuotaBytes = 1500 };
        private readonly User _alice;
        private readonly User _bob;

        public FileServiceTests()
        {
            _storage = new InMemoryObjectStorage(_time);
            _db = new PostgresContext(new DbContextOptionsBuilder<PostgresContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            _alice = new User("Alice") { UserName = "alice" };
            _bob = new User("Bob") { UserName = "bob" };
            _db.Users.AddRange(_alice, _bob);
            _db.SaveChanges();
        }

        public void Dispose() => _db.Dispose();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private FileService Files() => new(_db, _storage, Options.Create(_options), _time, NullLogger<FileService>.Instance);

        private static MemoryStream Text(string value) => new(Encoding.UTF8.GetBytes(value));

        private async Task<int> ObjectCount()
        {
            var count = 0;
            await foreach (var _ in _storage.ListAsync(FileService.KeyPrefix, Ct))
                count++;
            return count;
        }

        [Fact]
        public async Task Upload_StoresTheBytesAndAReadyRecord()
        {
            var result = await Files().UploadAsync(_alice.Id, FilePurpose.Attachment, "notes.txt", Text("hello"), 5, Ct);

            Assert.True(result.Successful);
            var file = result.Value!;
            Assert.Equal(FileStatus.Ready, file.Status);
            Assert.Equal(5, file.Size);
            Assert.Equal("text/plain", file.ContentType);
            Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", file.Sha256);
            Assert.Equal($"files/{file.Id:N}", file.StorageKey);
            Assert.NotNull(await _storage.HeadAsync(file.StorageKey, Ct));
        }

        [Fact]
        public async Task Upload_KeepsTheNameButNotItsPath()
        {
            var result = await Files().UploadAsync(_alice.Id, FilePurpose.Attachment, "../../etc/\u0007passwd", Text("x"), 1, Ct);

            Assert.Equal("passwd", result.Value!.Name);
        }

        [Fact]
        public async Task Upload_TypeComesFromTheContent_NotTheName()
        {
            var result = await Files().UploadAsync(_alice.Id, FilePurpose.Attachment, "page.html", Text("<script>alert(1)</script>"), null, Ct);

            Assert.Equal("text/plain", result.Value!.ContentType);
        }

        [Fact]
        public async Task Upload_AnAvatarThatIsNotAnImage_IsRefusedAndLeavesNothing()
        {
            var result = await Files().UploadAsync(_alice.Id, FilePurpose.Avatar, "me.png", Text("not a picture"), 13, Ct);

            Assert.Equal(FileErrors.UnsupportedType, result.Error.Code);
            Assert.Empty(_db.StoredFiles);
            Assert.Equal(0, await ObjectCount());
        }

        [Fact]
        public async Task Upload_OverTheLimitOfItsPurpose_IsRefused_KnownLengthOrNot()
        {
            var declared = await Files().UploadAsync(_alice.Id, FilePurpose.Avatar, "a.png", new MemoryStream(Png), 70, Ct);
            var streamed = await Files().UploadAsync(_alice.Id, FilePurpose.Attachment, "a.txt", Text(new string('a', 1001)), null, Ct);

            Assert.Equal(FileErrors.TooLarge, declared.Error.Code);
            Assert.Equal(FileErrors.TooLarge, streamed.Error.Code);
            Assert.Empty(_db.StoredFiles);
            Assert.Equal(0, await ObjectCount());
        }

        [Fact]
        public async Task Upload_OverTheQuota_IsRefused()
        {
            await Files().UploadAsync(_alice.Id, FilePurpose.Attachment, "a.txt", Text(new string('a', 900)), 900, Ct);

            var declared = await Files().UploadAsync(_alice.Id, FilePurpose.Attachment, "b.txt", Text(new string('b', 700)), 700, Ct);
            var streamed = await Files().UploadAsync(_alice.Id, FilePurpose.Attachment, "b.txt", Text(new string('b', 700)), null, Ct);
            var others = await Files().UploadAsync(_bob.Id, FilePurpose.Attachment, "b.txt", Text(new string('b', 700)), 700, Ct);

            Assert.Equal(FileErrors.QuotaExceeded, declared.Error.Code);
            Assert.Equal(FileErrors.QuotaExceeded, streamed.Error.Code);
            Assert.True(others.Successful);
            Assert.Equal(2, await ObjectCount());
        }

        [Fact]
        public async Task Upload_WithoutObjectStorage_IsNotConfigured()
        {
            var files = new FileService(_db, new UnconfiguredStorage(), Options.Create(_options), _time, NullLogger<FileService>.Instance);

            var result = await files.UploadAsync(_alice.Id, FilePurpose.Attachment, "a.txt", Text("x"), 1, Ct);

            Assert.Equal(FileErrors.NotConfigured, result.Error.Code);
        }

        [Fact]
        public async Task Get_OwnFilesAndOthersAvatars_ButNotOthersFiles()
        {
            var attachment = (await Files().UploadAsync(_alice.Id, FilePurpose.Attachment, "a.txt", Text("x"), 1, Ct)).Value!;
            var avatar = (await Files().SetAvatarAsync(_alice.Id, "me.png", new MemoryStream(Png), Png.Length, Ct)).Value!;

            Assert.True((await Files().GetAsync(_alice.Id, attachment.Id, Ct)).Successful);
            Assert.True((await Files().GetAsync(_bob.Id, avatar.Id, Ct)).Successful);
            Assert.Equal(FileErrors.NotFound, (await Files().GetAsync(_bob.Id, attachment.Id, Ct)).Error.Code);
        }

        [Fact]
        public async Task SetAvatar_ReplacesAndDeletesThePreviousOne()
        {
            var first = (await Files().SetAvatarAsync(_alice.Id, "1.png", new MemoryStream(Png), Png.Length, Ct)).Value!;
            var second = (await Files().SetAvatarAsync(_alice.Id, "2.png", new MemoryStream(Png), Png.Length, Ct)).Value!;

            Assert.Equal(second.Id, (await _db.Users.SingleAsync(u => u.Id == _alice.Id, Ct)).AvatarFileId);
            Assert.False(await _db.StoredFiles.AnyAsync(f => f.Id == first.Id, Ct));
            Assert.Null(await _storage.HeadAsync(first.StorageKey, Ct));
        }

        [Fact]
        public async Task RemoveAvatar_UnsetsAndDeletesIt()
        {
            var avatar = (await Files().SetAvatarAsync(_alice.Id, "1.png", new MemoryStream(Png), Png.Length, Ct)).Value!;

            Assert.True((await Files().RemoveAvatarAsync(_alice.Id, Ct)).Successful);
            Assert.True((await Files().RemoveAvatarAsync(_alice.Id, Ct)).Successful);

            Assert.Null((await _db.Users.SingleAsync(u => u.Id == _alice.Id, Ct)).AvatarFileId);
            Assert.Null(await _storage.HeadAsync(avatar.StorageKey, Ct));
        }

        [Fact]
        public async Task List_OnlyOwnReadyFiles_APageAfterTheCursor()
        {
            var ids = new List<Guid>();
            for (var i = 0; i < 3; i++)
                ids.Add((await Files().UploadAsync(_alice.Id, FilePurpose.Attachment, $"{i}.txt", Text("x"), 1, Ct)).Value!.Id);
            await Files().UploadAsync(_alice.Id, FilePurpose.Import, "data.json", Text("{}"), 2, Ct);
            await Files().UploadAsync(_bob.Id, FilePurpose.Attachment, "bob.txt", Text("x"), 1, Ct);

            var first = await Files().ListAsync(_alice.Id, FilePurpose.Attachment, null, 2, Ct);
            var rest = await Files().ListAsync(_alice.Id, FilePurpose.Attachment, first[^1].Id, 2, Ct);

            Assert.Equal(ids, first.Concat(rest).Select(f => f.Id));
            Assert.Equal(4, (await Files().ListAsync(_alice.Id, null, null, 10, Ct)).Count);
        }

        [Fact]
        public async Task Delete_OnlyTheOwner_RemovesRowAndBytes()
        {
            var file = (await Files().UploadAsync(_alice.Id, FilePurpose.Attachment, "a.txt", Text("x"), 1, Ct)).Value!;

            Assert.Equal(FileErrors.NotFound, (await Files().DeleteAsync(_bob.Id, file.Id, Ct)).Error.Code);
            Assert.True((await Files().DeleteAsync(_alice.Id, file.Id, Ct)).Successful);

            Assert.Empty(_db.StoredFiles);
            Assert.Equal(0, await ObjectCount());
        }

        [Fact]
        public async Task Open_ASpan_ReturnsThoseBytes()
        {
            var file = (await Files().UploadAsync(_alice.Id, FilePurpose.Attachment, "a.txt", Text("0123456789"), 10, Ct)).Value!;

            await using var stored = (await Files().OpenAsync(file, new ByteSpan(2, 5), Ct)).Value!;
            using var reader = new StreamReader(stored.Content);

            Assert.Equal("2345", await reader.ReadToEndAsync(Ct));
        }

        [Fact]
        public async Task Prune_RemovesStaleUploadsAndOrphans_NotRecentOnes()
        {
            var kept = (await Files().UploadAsync(_alice.Id, FilePurpose.Attachment, "kept.txt", Text("x"), 1, Ct)).Value!;
            _db.StoredFiles.Add(new StoredFile { OwnerId = _alice.Id, Name = "stale", StorageKey = "files/stale", CreatedAt = _time.GetUtcNow().UtcDateTime.AddDays(-2) });
            _db.StoredFiles.Add(new StoredFile { OwnerId = _alice.Id, Name = "fresh", StorageKey = "files/fresh", CreatedAt = _time.GetUtcNow().UtcDateTime });
            await _db.SaveChangesAsync(Ct);
            await _storage.PutAsync("files/orphan", Text("x"), null, Ct);
            _time.Advance(TimeSpan.FromDays(2));
            await _storage.PutAsync("files/recent-orphan", Text("x"), null, Ct);

            var dry = await Files().PruneAsync(dryRun: true, Ct);
            var done = await Files().PruneAsync(dryRun: false, Ct);

            Assert.Equal((2, 1), dry);
            Assert.Equal((2, 1), done);
            Assert.Equal([kept.Id], await _db.StoredFiles.Select(f => f.Id).ToListAsync(Ct));
            Assert.Null(await _storage.HeadAsync("files/orphan", Ct));
            Assert.NotNull(await _storage.HeadAsync("files/recent-orphan", Ct));
        }

        private sealed class UnconfiguredStorage : IObjectStorage
        {
            public bool IsConfigured => false;
            public Task CheckAsync(CancellationToken cancellationToken) => throw new ObjectStorageNotConfiguredException();
            public Task PutAsync(string key, Stream content, string? contentType, CancellationToken cancellationToken) => throw new ObjectStorageNotConfiguredException();
            public Task<StoredObject?> GetAsync(string key, CancellationToken cancellationToken, ByteSpan? range = null) => throw new ObjectStorageNotConfiguredException();
            public Task<ObjectInfo?> HeadAsync(string key, CancellationToken cancellationToken) => throw new ObjectStorageNotConfiguredException();
            public Task DeleteAsync(string key, CancellationToken cancellationToken) => throw new ObjectStorageNotConfiguredException();
            public IAsyncEnumerable<ObjectInfo> ListAsync(string? prefix, CancellationToken cancellationToken) => throw new ObjectStorageNotConfiguredException();
        }
    }

    public class FileContentTests
    {
        [Theory]
        [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "a.bin", "image/png")]
        [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "a", "image/jpeg")]
        [InlineData(new byte[] { 0x50, 0x4B, 3, 4, 0 }, "a.txt", "application/zip")]
        [InlineData(new byte[] { 0x1F, 0x8B, 8 }, "a", "application/gzip")]
        [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, "a", "application/pdf")]
        [InlineData(new byte[] { 0x7B, 0x7D }, "data.json", "application/json")]
        [InlineData(new byte[] { 0x61, 0x2C, 0x62 }, "data.csv", "text/csv")]
        [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67, 0x3E }, "icon.svg", "text/plain")]
        [InlineData(new byte[] { 0, 1, 2, 3 }, "a.txt", "application/octet-stream")]
        public void Detect_FromTheFirstBytes(byte[] head, string name, string expected)
        {
            Assert.Equal(expected, FileContent.Detect(head, name));
        }

        [Fact]
        public void Detect_WebP()
        {
            Assert.Equal("image/webp", FileContent.Detect("RIFF\0\0\0\0WEBPVP8 "u8, "a"));
        }

        [Fact]
        public void Detect_TextCutInsideACharacter_IsStillText()
        {
            var bytes = Encoding.UTF8.GetBytes("привет");
            Assert.Equal("text/plain", FileContent.Detect(bytes.AsSpan(0, bytes.Length - 1), "a.txt"));
        }

        [Theory]
        [InlineData(FilePurpose.Avatar, "image/png", true)]
        [InlineData(FilePurpose.Avatar, "image/gif", false)]
        [InlineData(FilePurpose.Import, "application/json", true)]
        [InlineData(FilePurpose.Import, "application/pdf", false)]
        [InlineData(FilePurpose.Attachment, "application/octet-stream", true)]
        public void Accepts_PerPurpose(FilePurpose purpose, string type, bool accepted)
        {
            Assert.Equal(accepted, FileContent.Accepts(purpose, type));
        }

        [Theory]
        [InlineData("bytes=0-99", 1000, 0, 99)]
        [InlineData("bytes=900-", 1000, 900, 999)]
        [InlineData("bytes=-100", 1000, 900, 999)]
        [InlineData("bytes=990-2000", 1000, 990, 999)]
        public void ByteRanges_SatisfiableRanges(string header, long size, long from, long to)
        {
            Assert.Equal(new ByteSpan(from, to), ByteRanges.Parse(header, size));
        }

        [Theory]
        [InlineData("bytes=1000-", 1000)]
        [InlineData("bytes=0-1,5-9", 1000)]
        [InlineData("items=0-1", 1000)]
        [InlineData("bytes=-0", 1000)]
        [InlineData("bytes=0-0", 0)]
        public void ByteRanges_UnsatisfiableOrUnsupported_IsNull(string header, long size)
        {
            Assert.Null(ByteRanges.Parse(header, size));
        }
    }
}
