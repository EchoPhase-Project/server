using EchoPhase.Attributes;
using EchoPhase.Helpers;
using EchoPhase.Interfaces;
using EchoPhase.Runners.Enums;
using EchoPhase.Runners.Params;

namespace EchoPhase.Runners.Handlers
{
    [BlockTypeHandler(BlockTypes.If)]
    public class IfHandler : BlockHandlerBase<IfParams>
    {
        public IfHandler(IServiceProvider serviceProvider)
            : base(serviceProvider) { }

        public override Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, IfParams param)
        {
            bool conditionResult = ExpressionHelper.EvaluateConditionWithVariables(param.Condition, context.Variables);
            var next = conditionResult ? param.TrueNext : param.FalseNext;
            return Task.FromResult(next);
        }
    }
}
