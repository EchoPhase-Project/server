using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Dto.Auth
{
    public record LoginDto(
        [Required][MinLength(3)] string Username,
        [Required][MinLength(6)] string Password
    );
}
