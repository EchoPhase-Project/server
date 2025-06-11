using EchoPhase.Helpers;
using EchoPhase.Interfaces;
using EchoPhase.Runners;
using EchoPhase.Runners.Models;
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
        private readonly ProjectionHelper _projection;

        public BlocksController(
            BlocksRunner runner,
            IServiceProvider serviceProvider,
            ProjectionHelper projection
        )
        {
            _runner = runner;
            _serviceProvider = serviceProvider;
            _projection = projection;
        }

        [HttpPost("run")]
        public async Task<IActionResult> RunBlocks([FromBody] RunBlocksRequest request)
        {
            IBlockExecutionContext context = new BlockExecutionContext
            {
                StartIds = request.StartIds,
                Blocks = request.Blocks
            };

            context = await _runner.ExecuteAsync(context);
            return Ok(context);
        }
    }

    public class RunBlocksRequest
    {
        public IEnumerable<int> StartIds { get; set; } = new HashSet<int>();
        public IEnumerable<IBlock> Blocks { get; set; } = new HashSet<Block>();
    }
}
