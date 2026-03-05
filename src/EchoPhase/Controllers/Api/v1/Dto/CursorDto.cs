using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Dto
{
    public record CursorDto(
        string? After,
        [Range(20, 80)] int Limit = 20
    );
}
