namespace EchoPhase.Runners.Blocks.Models
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
