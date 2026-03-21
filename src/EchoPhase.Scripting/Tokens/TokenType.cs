// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Tokens
{
    public enum TokenType
    {
        Number, Identifier,
        Plus, Minus, Star, Slash, Mod,
        Equal, NotEqual, Less, Greater, LessEq, GreaterEq,
        And, Or, Not,
        Question, Colon,
        LParen, RParen, Comma,
        String,
        End
    }
}
