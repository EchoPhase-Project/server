using System.ComponentModel.DataAnnotations;
using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Controllers.Api.v1.Dto.Blocks
{
    public class RunBlocksRequest
    {
        [Required]
        public HashSet<int> StartIds { get; set; } = [];

        [Required]
        [MinLength(1)]
        public List<Block> Blocks { get; set; } = [];
    }
}
