using EchoPhase.Projection;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1/blocks")]
    public class BlocksController : ControllerBase
    {
        private readonly BlocksRunner _runner;
        private readonly IServiceProvider _serviceProvider;
        private readonly Projector _projector;

        public BlocksController(
            BlocksRunner runner,
            IServiceProvider serviceProvider,
            Projector projector
        )
        {
            _runner = runner;
            _serviceProvider = serviceProvider;
            _projector = projector;
        }

        [HttpPost("run")]
        public async Task<IActionResult> RunBlocks([FromBody] RunBlocksRequest request)
        {
            var context = BlocksRunner.Create(request.StartIds, request.Blocks);

            context = await _runner.ExecuteAsync(context);
            return Ok(_projector.Project(context, ctx => ctx.Errors, ctx => ctx.Output, ctx => ctx.Variables));
        }
    }

    public class RunBlocksRequest
    {
        public IEnumerable<int> StartIds { get; set; } = new HashSet<int>();
        public IEnumerable<IBlock> Blocks { get; set; } = new HashSet<Block>();
    }
}
