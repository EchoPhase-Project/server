using EchoPhase.Attributes;
using EchoPhase.Helpers;
using EchoPhase.Interfaces;
using EchoPhase.Runners.Enums;
using EchoPhase.Runners.Params;

namespace EchoPhase.Runners.Handlers
{
    [BlockTypeHandler(BlockTypes.Print)]
    public class PrintHandler : BlockHandlerBase<PrintParams>
    {
        public PrintHandler(IServiceProvider serviceProvider)
            : base(serviceProvider) { }

        public override Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, PrintParams param)
        {
            var result = ExpressionHelper.ProcessStringWithExpressions(param.Text, context.Variables);
            context.Output.Add(result);
            return Task.FromResult(param.Next);
        }
    }
}
