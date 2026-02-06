namespace EchoPhase.DAL.Scylla.Cql
{
    public enum TokenType
    {
        Keyword,
        Identifier,
        String,
        Number,
        Operator,
        OpenParen,
        CloseParen,
        Comma,
        Semicolon,
        Whitespace,
        Unknown
    }
}
