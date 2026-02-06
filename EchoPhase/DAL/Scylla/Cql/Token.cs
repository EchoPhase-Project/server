namespace EchoPhase.DAL.Scylla.Cql
{
    public class Token
    {
        public TokenType Type { get; set; }
        public string Value { get; set; } = string.Empty;

        public Token(TokenType type, string value)
        {
            Type = type;
            Value = value;
        }
    }
}
