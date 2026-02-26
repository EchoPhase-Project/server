using EchoPhase.Profilers;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;
using EchoPhase.Scripting.Lexers;
using EchoPhase.Scripting.Parsers;
using EchoPhase.Scripting.Tokens;
using EchoPhase.Types.Result.Extensions;

namespace EchoPhase.Runners.Blocks.Handlers
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
