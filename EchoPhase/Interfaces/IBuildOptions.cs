namespace EchoPhase.Interfaces
{
    public interface IBuildOptions
    {
        public bool IncludeProperties
        {
            get; set;
        }
        public bool IncludeFields
        {
            get; set;
        }
    }
}
