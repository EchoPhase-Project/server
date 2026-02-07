namespace EchoPhase.Scripting.Lexers
{
    public interface ILexer<TToken>
    {
        IList<TToken> Tokens
        {
            get;
        }
        void With(string input);
    }
}
