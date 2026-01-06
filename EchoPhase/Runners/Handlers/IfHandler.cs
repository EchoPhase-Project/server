using EchoPhase.Attributes;
using EchoPhase.Expressions;
using EchoPhase.Expressions.Tokens;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;
using EchoPhase.Runners.Enums;
using EchoPhase.Runners.Params;

namespace EchoPhase.Runners.Handlers
{
    [BlockTypeHandler(BlockTypes.If)]
    public class IfHandler : BlockHandlerBase<IfParams>
    {
        private readonly IProfiler _profiler;
        private readonly ILexer<Token> _lexer;
        private readonly IParser<Token> _parser;

        public IfHandler(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
            _profiler = GetService<IProfiler>();
            _lexer = GetService<ILexer<Token>>();
            _parser = GetService<IParser<Token>>();
        }

        public override Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, IfParams param)
        {
            var result = Eval.Execute<bool>(_lexer, _parser, param.Condition, context.Variables);

            if (!result.TryGetValue(out var flag))
                if (result.TryGetError(out var err))
                    throw new Exception(err.Message);
                else
                    throw new Exception("Unknown error");

            var next = flag ? param.TrueNext : param.FalseNext;
            return Task.FromResult(next);
        }
    }
}
