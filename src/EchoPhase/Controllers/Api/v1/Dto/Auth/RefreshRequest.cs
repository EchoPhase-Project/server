using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Dto.Auth
{
    public record RefreshRequest(
        [Required] string DeviceId,
        [Required] string RefreshToken
    );
}
