using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Dto.Discord
{
    public record SetVaultDto(
       [Required][MinLength(6)] string Name,
       [Required] string Value
    );
}
