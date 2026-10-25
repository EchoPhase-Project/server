// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using EchoPhase.Configuration.Storage.Files;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Files;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
using EchoPhase.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace EchoPhase.Server.Controllers.Api.v1.Files
{
    /// <param name="Id">The file's id.</param>
    /// <param name="Purpose">What it is for: decides its limits and who may read it.</param>
    /// <param name="Name">The name it was uploaded with.</param>
    /// <param name="ContentType">The type found in its content.</param>
    /// <param name="Size">Bytes.</param>
    /// <param name="Sha256">Hex SHA-256 of the content.</param>
    /// <param name="CreatedAt">When it was uploaded.</param>
    public sealed record FileResponse(Guid Id, FilePurpose Purpose, string Name, string ContentType, long Size,
        string Sha256, DateTime CreatedAt)
    {
        public static FileResponse From(StoredFile file) =>
            new(file.Id, file.Purpose, file.Name, file.ContentType, file.Size, file.Sha256, file.CreatedAt);
    }

    public sealed class UploadFileRequest
    {
        /// <summary>The file.</summary>
        public required IFormFile File { get; set; }

        /// <summary>Import or Attachment (avatars: PUT /api/v1/auth/me/avatar; artifacts come from workflows).</summary>
        public FilePurpose Purpose { get; set; } = FilePurpose.Attachment;
    }

    /// <summary>
    /// The caller's files in object storage: upload, list, fetch and delete.
    /// Content is served with the type found in it, as a download unless it
    /// is a raster image, and never sniffed by the browser.
    /// </summary>
    [Route("api/v1/files")]
    public class FilesController : ApiControllerBase
    {
        private readonly FileService _files;

        public FilesController(FileService files)
        {
            _files = files;
        }

        /// <summary>Uploads a file (multipart/form-data: file, purpose).</summary>
        [HttpPost]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Files, Permissions.Create)]
        [RequestSizeLimit(FilesOptions.MaxUploadBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = FilesOptions.MaxUploadBytes)]
        [Consumes("multipart/form-data")]
        [ProducesResponseType<FileResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Upload([FromForm] UploadFileRequest dto, CancellationToken ct)
        {
            if (dto.Purpose is FilePurpose.Avatar or FilePurpose.Artifact)
                return Error(FileErrors.Invalid,
                    "Avatars are uploaded with PUT /api/v1/auth/me/avatar; artifacts come from workflows.",
                    StatusCodes.Status400BadRequest);

            await using var content = dto.File.OpenReadStream();
            var result = await _files.UploadAsync(User.GetUserId(), dto.Purpose, dto.File.FileName, content, dto.File.Length, ct);
            if (!result.Successful)
                return Failure(result.Error);
            return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, FileResponse.From(result.Value));
        }

        /// <summary>The caller's files, oldest first, a page at a time: pass the last id as `after`.</summary>
        [HttpGet]
        [RequirePermission(Resources.Files, Permissions.Read)]
        [ProducesResponseType<FileResponse[]>(StatusCodes.Status200OK)]
        public async Task<IActionResult> List(FilePurpose? purpose, Guid? after,
            [Range(1, 1000)] int limit = 100, CancellationToken ct = default)
        {
            var files = await _files.ListAsync(User.GetUserId(), purpose, after, limit, ct);
            return Ok(files.Select(FileResponse.From));
        }

        [HttpGet("{id:guid}")]
        [RequirePermission(Resources.Files, Permissions.Read)]
        [ProducesResponseType<FileResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var result = await _files.GetAsync(User.GetUserId(), id, ct);
            return result.Successful ? Ok(FileResponse.From(result.Value)) : Failure(result.Error);
        }

        /// <summary>
        /// The file's content. Supports a single byte range (Range: bytes=…)
        /// and If-None-Match; a file never changes, so it may be cached long.
        /// </summary>
        [HttpGet("{id:guid}/content")]
        [RequirePermission(Resources.Files, Permissions.Read)]
        [Produces("application/octet-stream", "application/json")]
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, "application/octet-stream")]
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status206PartialContent, "application/octet-stream")]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status416RangeNotSatisfiable)]
        public async Task<IActionResult> Content(Guid id, CancellationToken ct)
        {
            var found = await _files.GetAsync(User.GetUserId(), id, ct);
            if (!found.Successful)
                return Failure(found.Error);
            var file = found.Value;

            var etag = new EntityTagHeaderValue($"\"{file.Sha256}\"");
            var headers = Response.GetTypedHeaders();
            headers.ETag = etag;
            headers.CacheControl = new CacheControlHeaderValue { Private = true, MaxAge = TimeSpan.FromDays(365) };
            Response.Headers.AcceptRanges = "bytes";
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";

            if (Request.GetTypedHeaders().IfNoneMatch.Any(tag => tag.Compare(etag, useStrongComparison: false)))
                return StatusCode(StatusCodes.Status304NotModified);

            ByteSpan? range = null;
            if (Request.Headers.Range.Count > 0)
            {
                range = ByteRanges.Parse(Request.Headers.Range.ToString(), file.Size);
                if (range is null)
                {
                    Response.Headers.ContentRange = $"bytes */{file.Size}";
                    return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
                }
            }

            var opened = await _files.OpenAsync(file, range, ct);
            if (!opened.Successful)
                return Failure(opened.Error);

            var disposition = new ContentDispositionHeaderValue(FileContent.IsInline(file.ContentType) ? "inline" : "attachment");
            disposition.SetHttpFileName(file.Name);
            Response.Headers.ContentDisposition = disposition.ToString();

            await using var stored = opened.Value;
            Response.ContentType = file.ContentType;
            if (range is { } span)
            {
                Response.StatusCode = StatusCodes.Status206PartialContent;
                Response.Headers.ContentRange = $"bytes {span.From}-{span.To}/{file.Size}";
                Response.ContentLength = span.Length;
            }
            else
            {
                Response.ContentLength = file.Size;
            }
            await stored.Content.CopyToAsync(Response.Body, ct);
            return new EmptyResult();
        }

        [HttpDelete("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Files, Permissions.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var result = await _files.DeleteAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }
    }

    /// <summary>The one byte range of a Range header, within a file's size.</summary>
    public static class ByteRanges
    {
        /// <summary>
        /// The span asked for (bytes=a-b, bytes=a-, bytes=-n), clipped to the
        /// size; null when it is not satisfiable or not a single byte range.
        /// </summary>
        public static ByteSpan? Parse(string header, long size)
        {
            if (size == 0 || !RangeHeaderValue.TryParse(header, out var value) ||
                !value.Unit.Equals("bytes", StringComparison.OrdinalIgnoreCase) || value.Ranges.Count != 1)
                return null;

            var range = value.Ranges.Single();
            long from, to;
            if (range.From is null)
            {
                if (range.To is not { } suffix || suffix == 0)
                    return null;
                from = Math.Max(0, size - suffix);
                to = size - 1;
            }
            else
            {
                from = range.From.Value;
                to = Math.Min(range.To ?? size - 1, size - 1);
            }
            return from < size && from <= to ? new ByteSpan(from, to) : null;
        }
    }
}
