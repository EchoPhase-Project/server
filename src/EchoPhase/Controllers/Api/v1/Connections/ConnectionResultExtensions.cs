// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Connections;
using EchoPhase.Controllers.Api.v1.Common;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1.Connections
{
    public static class ConnectionResultExtensions
    {
        /// <summary>
        /// Maps a failure from the connections layer to an HTTP response.
        /// </summary>
        public static IActionResult ConnectionError(this ControllerBase controller, IServiceError error)
        {
            var status = error.Code switch
            {
                ConnectionErrors.NotFound => StatusCodes.Status404NotFound,
                ConnectionErrors.Conflict => StatusCodes.Status409Conflict,
                ConnectionErrors.ReauthRequired => StatusCodes.Status409Conflict,
                ConnectionErrors.Forbidden => StatusCodes.Status403Forbidden,
                ConnectionErrors.Upstream => StatusCodes.Status502BadGateway,
                ConnectionErrors.NotConfigured => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status400BadRequest
            };

            return controller.StatusCode(status, ErrorBody.From(error));
        }
    }
}
