namespace EchoPhase.Interfaces
{
    public interface IPathParser<TToken>
    {
        void With(ILexer<TToken> lexer, IDictionary<string, object> variables);
        object? Resolve();
        IDictionary<string, object> Set(object? value);
    }
}
