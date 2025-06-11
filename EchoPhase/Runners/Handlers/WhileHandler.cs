using EchoPhase.Attributes;
using EchoPhase.Helpers;
using EchoPhase.Interfaces;
using EchoPhase.Runners.Enums;
using EchoPhase.Runners.Params;

namespace EchoPhase.Runners.Handlers
{
    [BlockTypeHandler(BlockTypes.While)]
    public class WhileHandler : BlockHandlerBase<WhileParams>
    {
        private readonly BlocksRunner _runner;

        public WhileHandler(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
            _runner = GetService<BlocksRunner>();
        }

        public override async Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, WhileParams param)
        {
            var originalStartIds = context.StartIds;

            try
            {
                while (ExpressionHelper.EvaluateConditionWithVariables(param.Condition, context.Variables))
                {
                    context.StartIds = param.BodyNext;
                    await _runner.ExecuteAsync(context);
                }
            }
            finally
            {
                context.StartIds = originalStartIds;
            }

            return param.AfterNext;
        }
    }
}
