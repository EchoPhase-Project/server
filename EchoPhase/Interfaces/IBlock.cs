using EchoPhase.Runners.Enums;

namespace EchoPhase.Interfaces
{
    public interface IBlock
    {
        public int Id
        {
            get; set;
        }
        public BlockTypes Type
        {
            get; set;
        }
        public object Params
        {
            get; set;
        }
    }
}
