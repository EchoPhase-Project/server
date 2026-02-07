using EchoPhase.Profilers;
using EchoPhase.Types.Extensions;
using EchoPhase.Scripting;
using EchoPhase.Scripting.Lexers;
using EchoPhase.Scripting.Tokens;
using EchoPhase.Scripting.Parsers;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Runners.Blocks.Contexts;

namespace EchoPhase.Runners.Blocks.Handlers
{
    [BlockTypeHandler(BlockTypes.Print)]
    public class PrintHandler : BlockHandlerBase<PrintParams>
    {
        private readonly IProfiler _profiler;
        private readonly ILexer<TemplateToken> _lexer;
        private readonly IParser<TemplateToken> _parser;

        public PrintHandler(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
            _profiler = GetService<IProfiler>();
            _lexer = GetService<ILexer<TemplateToken>>();
            _parser = GetService<IParser<TemplateToken>>();
        }

        public override Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, PrintParams param)
        {
            var result = Eval.Process<string>(_lexer, _parser, param.Text, context.Variables);

            if (!result.TryGetValue(out var output))
                if (result.TryGetError(out var err))
                    throw new Exception(err.Message);
                else
                    throw new Exception("Unknown error");

            context.Output.Add(output);
            return Task.FromResult(param.Next);
        }
    }
}
