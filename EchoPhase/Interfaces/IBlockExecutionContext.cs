using EchoPhase.Runners.Models;

namespace EchoPhase.Interfaces
{
    public interface IBlockExecutionContext
    {
        public IEnumerable<int> StartIds
        {
            get; set;
        }
        public IEnumerable<IBlock> Blocks
        {
            get; set;
        }
        public IDictionary<string, object> Variables
        {
            get; set;
        }
        public IDictionary<string, int> LoopStates
        {
            get; set;
        }
        public IList<string> Output
        {
            get; set;
        }
        public IList<BlockError> Errors
        {
            get; set;
        }
    }
}
