using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Dto.Auth
{
    public record DeleteRefreshRequest(
        [Required] Guid RefreshId
    );
}

