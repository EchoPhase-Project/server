// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Scripting.Lexers;

namespace EchoPhase.Scripting.Parsers
{
    public interface IParser<TToken>
    {
        void With(ILexer<TToken> lexer, IDictionary<string, object> variables);
        T Parse<T>();
    }
}
