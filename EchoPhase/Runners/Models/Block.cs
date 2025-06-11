using EchoPhase.Interfaces;
using EchoPhase.Runners.Enums;

namespace EchoPhase.Runners.Models
{
    public class Block : IBlock
    {
        public int Id
        {
            get; set;
        }
        public BlockTypes Type
        {
            get; set;
        }
        public object Params { get; set; } = new();
    }
}
