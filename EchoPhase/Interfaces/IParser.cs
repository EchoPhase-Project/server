namespace EchoPhase.Interfaces
{
    public interface IParser<TToken>
    {
        void With(ILexer<TToken> lexer, IDictionary<string, object> variables);
        T Parse<T>();
    }
}
