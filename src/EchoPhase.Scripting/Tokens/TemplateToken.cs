namespace EchoPhase.Scripting.Tokens
{
    public class TemplateToken
    {
        public TemplateTokenType Type
        {
            get;
        }
        public string Value
        {
            get;
        }

        public TemplateToken(TemplateTokenType type, string value)
        {
            Type = type;
            Value = value;
        }
    }
}
