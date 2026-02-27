using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Dto.Auth
{
    public record RegisterDto(
        [Required][MinLength(2)] string Name,
        [Required][MinLength(3)] string Username,
        [Required][MinLength(6)] string Password
    );
}
