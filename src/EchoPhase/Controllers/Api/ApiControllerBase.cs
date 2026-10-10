// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Controllers.Api.v1.Common;
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
        /// <summary>A failed service result as <see cref="ErrorBody"/> with <paramref name="status"/>.</summary>
        protected ObjectResult Error(IServiceError error, int status)
            => StatusCode(status, ErrorBody.From(error));

        /// <summary>An error the controller raises itself (no service result behind it).</summary>
        protected ObjectResult Error(string code, string message, int status)
            => StatusCode(status, new ErrorBody(code, message));
    }
}
