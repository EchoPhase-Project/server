using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Dto.Auth
{
    public record CreateDto(
        [Required][MinLength(12)] string DeviceId,
        ClaimsDto? Claims = null
    );
}
