using EchoPhase.Runners.Enums;

namespace EchoPhase.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BlockTypeHandlerAttribute : Attribute
    {
        /// <summary>
        /// The type of block this handler is associated with.
        /// </summary>
        public BlockTypes Type
        {
            get;
        }

        public BlockTypeHandlerAttribute(BlockTypes type)
        {
            Type = type;
        }
    }
}
