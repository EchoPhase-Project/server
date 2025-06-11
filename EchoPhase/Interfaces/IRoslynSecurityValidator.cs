namespace EchoPhase.Interfaces
{
    public interface IRoslynSecurityValidator
    {
        /// <summary>
        /// Validates code for allowed assemblies. Returs validation errors.
        /// </summary>
        IEnumerable<string> Validate(string code);
    }
}
