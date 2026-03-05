using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Dto.Auth
{
    public record RefreshRequest(
        [Required] Guid RefreshId,
        [Required] string RefreshToken
    );
}
