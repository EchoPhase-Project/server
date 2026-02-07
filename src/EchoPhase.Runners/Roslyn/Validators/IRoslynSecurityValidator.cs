namespace EchoPhase.Runners.Roslyn.Validators
{
    public interface ISecurityValidator
    {
        /// <summary>
        /// Validates code for allowed assemblies. Returs validation errors.
        /// </summary>
        IEnumerable<string> Validate(string code);
    }
}
