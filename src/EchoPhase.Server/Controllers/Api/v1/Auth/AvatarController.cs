// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using EchoPhase.Configuration.Storage.Files;
using EchoPhase.Files;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
using EchoPhase.Server.Controllers.Api.v1.Files;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Auth
{
    public sealed class UploadAvatarRequest
    {
        /// <summary>A PNG, JPEG or WebP picture.</summary>
        [Required]
        public IFormFile File { get; set; } = default!;
    }

    /// <summary>
    /// The caller's own picture: a file of purpose Avatar, which every
    /// signed-in user may see (MeResponse.AvatarFileId). Replacing it deletes
    /// the previous one.
    /// </summary>
    [Route("api/v1/auth/me/avatar")]
    public class AvatarController : ApiControllerBase
    {
        private readonly FileService _files;

        public AvatarController(FileService files)
        {
            _files = files;
        }

        [HttpPut]
        [ValidateAntiForgery]
        [RequirePrincipal(PrincipalKind.Session)]
        [RequirePermission(Resources.Files, Permissions.Create)]
        [RequestSizeLimit(FilesOptions.MaxUploadBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = FilesOptions.MaxUploadBytes)]
        [Consumes("multipart/form-data")]
        [ProducesResponseType<FileResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status415UnsupportedMediaType)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Put([FromForm] UploadAvatarRequest dto, CancellationToken ct)
        {
            await using var content = dto.File.OpenReadStream();
            var result = await _files.SetAvatarAsync(User.GetUserId(), dto.File.FileName, content, dto.File.Length, ct);
            return result.Successful ? Ok(FileResponse.From(result.Value!)) : Failure(result.Error);
        }

        [HttpDelete]
        [ValidateAntiForgery]
        [RequirePrincipal(PrincipalKind.Session)]
        [RequirePermission(Resources.Files, Permissions.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Delete(CancellationToken ct)
        {
            var result = await _files.RemoveAvatarAsync(User.GetUserId(), ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }
    }
}
