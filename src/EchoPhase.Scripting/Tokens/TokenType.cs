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
