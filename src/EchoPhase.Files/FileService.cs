// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Storage.Files;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Configurations;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Storage;
using EchoPhase.Types.Result;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EchoPhase.Files
{
    /// <summary>
    /// Users' files: a row in Postgres (what, whose, which purpose) and the
    /// bytes in object storage under files/{id}. An upload is a pending row
    /// until its bytes are stored; what never completes, prune removes.
    /// </summary>
    public class FileService
    {
        /// <summary>The prefix of every file's key in the bucket.</summary>
        public const string KeyPrefix = "files/";

        /// <summary>The share of the quota past which the owner is warned (once, on crossing it).</summary>
        public const int QuotaWarningPercent = 90;

        private readonly PostgresContext _db;
        private readonly IObjectStorage _storage;
        private readonly FilesOptions _options;
        private readonly TimeProvider _time;
        private readonly ILogger<FileService> _logger;
        private readonly IFileEvents? _events;

        public FileService(PostgresContext db, IObjectStorage storage, IOptions<FilesOptions> options,
            TimeProvider time, ILogger<FileService> logger, IFileEvents? events = null)
        {
            _db = db;
            _storage = storage;
            _options = options.Value;
            _time = time;
            _logger = logger;
            _events = events;
        }

        /// <summary>The limit of a file of this purpose.</summary>
        public long LimitFor(FilePurpose purpose) =>
            purpose == FilePurpose.Avatar ? _options.AvatarMaxBytes : _options.MaxFileBytes;

        /// <summary>
        /// Stores a file for its owner: checks the limits and the content
        /// against the purpose, then keeps its bytes and makes it ready.
        /// </summary>
        /// <param name="length">The size, when known in advance (checked before reading).</param>
        public async Task<IServiceResult<StoredFile>> UploadAsync(Guid ownerId, FilePurpose purpose, string name,
            Stream content, long? length, CancellationToken cancellationToken)
        {
            if (!_storage.IsConfigured)
                return Fail<StoredFile>(FileErrors.NotConfigured, "Object storage is not configured on this server.");

            var limit = LimitFor(purpose);
            if (length > limit)
                return TooLarge<StoredFile>(limit);

            var used = await UsedAsync(ownerId, cancellationToken);
            if (length is { } size && used + size > _options.QuotaBytes)
                return QuotaExceeded<StoredFile>();

            var head = new byte[FileContent.HeadLength];
            var headLength = await content.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken);
            name = CleanName(name);
            var contentType = FileContent.Detect(head.AsSpan(0, headLength), name);
            if (!FileContent.Accepts(purpose, contentType))
                return Fail<StoredFile>(FileErrors.UnsupportedType,
                    $"A file of type {contentType} cannot be uploaded as {purpose.ToString().ToLowerInvariant()}.");

            var file = new StoredFile
            {
                OwnerId = ownerId,
                Purpose = purpose,
                Name = name,
                ContentType = contentType,
                CreatedAt = _time.GetUtcNow().UtcDateTime,
                UpdatedAt = _time.GetUtcNow().UtcDateTime,
            };
            file.StorageKey = $"{KeyPrefix}{file.Id:N}";
            _db.StoredFiles.Add(file);
            await _db.SaveChangesAsync(cancellationToken);

            await using var measured = new MeasuringStream(head.AsMemory(0, headLength), content, limit);
            try
            {
                await _storage.PutAsync(file.StorageKey, measured, contentType, cancellationToken);
            }
            catch (FileTooLargeException)
            {
                await DiscardAsync(file);
                return TooLarge<StoredFile>(limit);
            }
            catch
            {
                await DiscardAsync(file);
                throw;
            }

            if (length is null && used + measured.Count > _options.QuotaBytes)
            {
                await DiscardAsync(file);
                return QuotaExceeded<StoredFile>();
            }

            file.Size = measured.Count;
            file.Sha256 = measured.Sha256;
            file.Status = FileStatus.Ready;
            file.UpdatedAt = _time.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync(cancellationToken);
            await WarnAboutQuotaAsync(ownerId, used, used + file.Size, cancellationToken);
            return ServiceResult<StoredFile>.Success(file);
        }

        /// <summary>
        /// A ready file the requester may read: their own, or anyone's avatar.
        /// Not found otherwise, so others' file ids reveal nothing.
        /// </summary>
        public async Task<IServiceResult<StoredFile>> GetAsync(Guid requesterId, Guid fileId, CancellationToken cancellationToken)
        {
            var file = await _db.StoredFiles.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == fileId && f.Status == FileStatus.Ready, cancellationToken);
            return file is not null && (file.OwnerId == requesterId || file.Purpose == FilePurpose.Avatar)
                ? ServiceResult<StoredFile>.Success(file)
                : NotFound<StoredFile>();
        }

        /// <summary>The owner's ready files, oldest first, a page after the given id.</summary>
        public async Task<IReadOnlyList<StoredFile>> ListAsync(Guid ownerId, FilePurpose? purpose, Guid? after,
            int limit, CancellationToken cancellationToken)
        {
            var query = _db.StoredFiles.AsNoTracking()
                .Where(f => f.OwnerId == ownerId && f.Status == FileStatus.Ready);
            if (purpose is { } kind)
                query = query.Where(f => f.Purpose == kind);
            if (after is { } cursor)
                query = query.Where(f => f.Id > cursor);
            return await query.OrderBy(f => f.Id).Take(limit).ToListAsync(cancellationToken);
        }

        /// <summary>The bytes of a file (all, or a span of them); dispose the result.</summary>
        public async Task<IServiceResult<StoredObject>> OpenAsync(StoredFile file, ByteSpan? range, CancellationToken cancellationToken)
        {
            if (!_storage.IsConfigured)
                return Fail<StoredObject>(FileErrors.NotConfigured, "Object storage is not configured on this server.");
            var stored = await _storage.GetAsync(file.StorageKey, cancellationToken, range);
            return stored is null ? NotFound<StoredObject>() : ServiceResult<StoredObject>.Success(stored);
        }

        /// <summary>Deletes one of the owner's files: its row first, then its bytes.</summary>
        public async Task<IServiceResult> DeleteAsync(Guid ownerId, Guid fileId, CancellationToken cancellationToken)
        {
            var file = await _db.StoredFiles.FirstOrDefaultAsync(f => f.Id == fileId && f.OwnerId == ownerId, cancellationToken);
            if (file is null)
                return ServiceResult.Failure(new ServiceError(FileErrors.NotFound, "File not found."));

            _db.StoredFiles.Remove(file);
            await _db.SaveChangesAsync(cancellationToken);
            await RemoveBytesAsync(file.StorageKey);
            return ServiceResult.Success();
        }

        /// <summary>Uploads the user's avatar; the previous one is deleted.</summary>
        public async Task<IServiceResult<StoredFile>> SetAvatarAsync(Guid userId, string name, Stream content, long? length,
            CancellationToken cancellationToken)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user is null)
                return NotFound<StoredFile>();

            var uploaded = await UploadAsync(userId, FilePurpose.Avatar, name, content, length, cancellationToken);
            if (!uploaded.Successful)
                return uploaded;

            var previous = user.AvatarFileId;
            user.AvatarFileId = uploaded.Value.Id;
            await _db.SaveChangesAsync(cancellationToken);
            if (previous is { } old)
                await DeleteAsync(userId, old, cancellationToken);
            return uploaded;
        }

        /// <summary>Removes the user's avatar; having none is not an error.</summary>
        public async Task<IServiceResult> RemoveAvatarAsync(Guid userId, CancellationToken cancellationToken)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user is null)
                return ServiceResult.Failure(new ServiceError(FileErrors.NotFound, "User not found."));
            if (user.AvatarFileId is not { } avatar)
                return ServiceResult.Success();

            user.AvatarFileId = null;
            await _db.SaveChangesAsync(cancellationToken);
            return await DeleteAsync(userId, avatar, cancellationToken);
        }

        /// <summary>
        /// What prune removes: uploads unfinished past the timeout, and objects
        /// under files/ with no row (left by a failed delete). Counts only,
        /// with <paramref name="dryRun"/>.
        /// </summary>
        public async Task<(int Pending, int Orphans)> PruneAsync(bool dryRun, CancellationToken cancellationToken)
        {
            var cutoff = _time.GetUtcNow().UtcDateTime - _options.PendingTimeout;
            var stale = await _db.StoredFiles
                .Where(f => f.Status == FileStatus.Pending && f.CreatedAt < cutoff)
                .ToListAsync(cancellationToken);

            var orphans = new List<string>();
            if (_storage.IsConfigured)
            {
                await foreach (var item in _storage.ListAsync(KeyPrefix, cancellationToken))
                {
                    // Recent objects may belong to an upload in progress.
                    if (item.LastModified is { } modified && modified.UtcDateTime >= cutoff)
                        continue;
                    if (!await _db.StoredFiles.AnyAsync(f => f.StorageKey == item.Key, cancellationToken))
                        orphans.Add(item.Key);
                }
            }

            if (!dryRun)
            {
                _db.StoredFiles.RemoveRange(stale);
                await _db.SaveChangesAsync(cancellationToken);
                foreach (var key in stale.Select(f => f.StorageKey).Concat(orphans))
                    await RemoveBytesAsync(key);
            }
            return (stale.Count, orphans.Count);
        }

        private async Task<long> UsedAsync(Guid ownerId, CancellationToken cancellationToken) =>
            await _db.StoredFiles.Where(f => f.OwnerId == ownerId).SumAsync(f => f.Size, cancellationToken);

        // An upload that failed: its row goes, its bytes if any were stored.
        private async Task DiscardAsync(StoredFile file)
        {
            _db.StoredFiles.Remove(file);
            await _db.SaveChangesAsync(CancellationToken.None);
            await RemoveBytesAsync(file.StorageKey);
        }

        // Best effort: what is left behind, prune finds as an orphan.
        private async Task RemoveBytesAsync(string key)
        {
            try
            {
                if (_storage.IsConfigured)
                    await _storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete {Key} from object storage; prune will.", key);
            }
        }

        // Tells the owner once, when this upload crossed the warning line.
        private async Task WarnAboutQuotaAsync(Guid ownerId, long before, long after, CancellationToken ct)
        {
            if (_events is null)
                return;
            var line = _options.QuotaBytes * QuotaWarningPercent / 100;
            if (before >= line || after < line)
                return;
            try
            {
                await _events.QuotaNearlyFullAsync(ownerId, (int)Math.Min(100, after * 100 / _options.QuotaBytes), ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Stored already: only the warning is lost.
                _logger.LogWarning(e, "Could not warn user {UserId} about their storage quota", ownerId);
            }
        }

        // The name as shown: its last path segment, no control characters, at most the column's length.
        private static string CleanName(string name)
        {
            var last = name.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
            var clean = new string(last.Where(c => !char.IsControl(c)).ToArray()).Trim();
            if (clean.Length > StoredFileConfiguration.MaxNameLength)
                clean = clean[..StoredFileConfiguration.MaxNameLength];
            return clean.Length == 0 ? "file" : clean;
        }

        private static IServiceResult<T> Fail<T>(string code, string message) =>
            ServiceResult<T>.Failure(new ServiceError(code, message));

        private static IServiceResult<T> NotFound<T>() => Fail<T>(FileErrors.NotFound, "File not found.");

        /// <summary>The detail of <see cref="FileErrors.TooLarge"/>: the limit, in bytes.</summary>
        public const string LimitDetail = "limitBytes";

        private static IServiceResult<T> TooLarge<T>(long limit) =>
            ServiceResult<T>.Failure(new ServiceError(e =>
            {
                e.Set(FileErrors.TooLarge, $"The file is larger than this kind of file may be ({limit} bytes).");
                e.SetField("file");
                e.SetDetail(LimitDetail, limit);
            }));

        private static IServiceResult<T> QuotaExceeded<T>() =>
            Fail<T>(FileErrors.QuotaExceeded, "The file does not fit in what you may store; delete some files first.");
    }
}
