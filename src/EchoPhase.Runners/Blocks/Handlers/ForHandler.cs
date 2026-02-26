using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Params;

namespace EchoPhase.Runners.Blocks.Handlers
{
    [BlockTypeHandler(BlockTypes.For)]
    public class ForHandler : BlockHandlerBase<ForParams>
    {
        private readonly BlocksRunner _runner;

        public ForHandler(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
            _runner = GetService<BlocksRunner>();
        }

        public override async Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, ForParams param)
        {
            if (!context.LoopStates.TryGetValue(param.CounterVar, out int counter))
            {
                counter = param.Start;
                context.LoopStates[param.CounterVar] = counter;
            }

            while (true)
            {
                if ((param.Step > 0 && counter > param.End) || (param.Step < 0 && counter < param.End))
                    break;

                context.Variables[param.CounterVar] = counter;

                var originalStartIds = context.StartIds;

                try
                {
                    context.StartIds = param.BodyNext;
                    await _runner.ExecuteAsync(context);
                }
                finally
                {
                    context.StartIds = originalStartIds;
                }

                counter += param.Step;
                context.LoopStates[param.CounterVar] = counter;
            }

            context.LoopStates.Remove(param.CounterVar);
            context.Variables.Remove(param.CounterVar);

            return param.AfterNext;
        }
    }
}
