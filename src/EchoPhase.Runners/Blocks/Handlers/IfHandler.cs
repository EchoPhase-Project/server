// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

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

        public IfHandler(IProfiler profiler, ILexer<Token> lexer, IParser<Token> parser)
        {
            _profiler = profiler;
            _lexer = lexer;
            _parser = parser;
        }

        public override Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, IfParams param)
        {
            var result = Eval.Execute<bool>(_lexer, _parser, param.Condition, context.Variables);

            if (!result.TryGetValue(out var flag))
                if (result.TryGetError(out var err))
                    throw new InvalidOperationException(err.Message);
                else
                    throw new InvalidOperationException("Unknown error");

            var next = flag ? param.TrueNext : param.FalseNext;
            return Task.FromResult(next);
        }
    }
}
