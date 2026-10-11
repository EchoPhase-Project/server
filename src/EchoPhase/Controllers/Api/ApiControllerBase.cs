// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Connections;
using EchoPhase.Controllers.Api.v1.Common;
using EchoPhase.Security.Authentication;
using EchoPhase.Security.Authentication.ApiClients;
using EchoPhase.Services.Blocks;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api
{
    /// <summary>
    /// Base of the JSON API controllers: [ApiController] binding and validation,
    /// JSON only (keeps the OpenAPI document to one content type), and one way
    /// to turn a service failure into an <see cref="ErrorBody"/> response.
    /// </summary>
    [ApiController]
    [Produces("application/json")]
    public abstract class ApiControllerBase : ControllerBase
    {
        /// <summary>A failed service result as <see cref="ErrorBody"/> with the status its code means.</summary>
        protected ObjectResult Failure(IServiceError error)
            => Error(error, StatusFor(error));

        /// <summary>A failed service result as <see cref="ErrorBody"/> with <paramref name="status"/>.</summary>
        protected ObjectResult Error(IServiceError error, int status)
            => StatusCode(status, ErrorBody.From(error));

        /// <summary>
        /// The HTTP status of a service error: <see cref="ErrorCodes"/> shared by all
        /// layers, then the codes particular to one; anything else is a bad request.
        /// The cases are constants, so a renamed code changes here too, and two
        /// classes giving one value to different codes do not compile.
        /// </summary>
        public static int StatusFor(IServiceError error) => error.Code switch
        {
            ErrorCodes.NotFound => StatusCodes.Status404NotFound,
            ErrorCodes.Unauthorized or ErrorCodes.InvalidCredentials
                or AuthErrors.LockedOut or AuthErrors.InvalidRefreshToken
                or AuthErrors.RefreshTokenReused or ApiClientErrors.InvalidClient
                => StatusCodes.Status401Unauthorized,
            ErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
            ErrorCodes.Conflict or BlockRunErrors.AlreadyFinished or ConnectionErrors.ReauthRequired
                => StatusCodes.Status409Conflict,
            BlockRunErrors.TooManyActive => StatusCodes.Status429TooManyRequests,
            ConnectionErrors.Upstream => StatusCodes.Status502BadGateway,
            BlockRunErrors.QueueFull or ConnectionErrors.NotConfigured
                => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };

        /// <summary>An error the controller raises itself (no service result behind it).</summary>
        protected ObjectResult Error(string code, string message, int status)
            => StatusCode(status, new ErrorBody(code, message));
    }
}
