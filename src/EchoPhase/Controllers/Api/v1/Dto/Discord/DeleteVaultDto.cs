using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Dto.Discord
{
    public record DeleteVaultDto(
       [Required][MinLength(6)] string Name
    );
}
