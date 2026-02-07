using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Runners.Blocks.Contexts
{
    public class BlockExecutionContext : IBlockExecutionContext
    {
        public IEnumerable<int> StartIds { get; set; } = new HashSet<int>() { 0 };
        public IEnumerable<IBlock> Blocks { get; set; } = new HashSet<IBlock>();
        public IDictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
        public IDictionary<string, int> LoopStates { get; set; } = new Dictionary<string, int>();
        public IList<string> Output { get; set; } = new List<string>();
        public IList<BlockError> Errors { get; set; } = new List<BlockError>();
    }
}
