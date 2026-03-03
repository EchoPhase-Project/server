using EchoPhase.Controllers.Api.v1.Dto.Blocks;
using EchoPhase.Projection;
using EchoPhase.Runners.Blocks;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/blocks")]
    public class BlocksController : ControllerBase
    {
        private readonly BlocksRunner _runner;
        private readonly Projector _projector;

        public BlocksController(BlocksRunner runner, Projector projector)
        {
            _runner = runner;
            _projector = projector;
        }

        [HttpPost("run")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunBlocks([FromBody] RunBlocksRequest request)
        {
            if (request.Blocks.Count == 0)
                return BadRequest("At least one block is required.");

            var context = BlocksRunner.Create(request.StartIds, request.Blocks);
            context = await _runner.ExecuteAsync(context);

            return Ok(_projector.For(context).Include(ctx => ctx.Errors, ctx => ctx.Output, ctx => ctx.Variables).Build());
        }
    }
}
