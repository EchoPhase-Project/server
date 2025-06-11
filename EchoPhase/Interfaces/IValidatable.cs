namespace EchoPhase.Interfaces
{
    public interface IValidatable
    {
        public bool IsValid(out string errorMessage);
    }
}
