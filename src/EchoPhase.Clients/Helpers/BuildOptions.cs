namespace EchoPhase.Clients.Helpers
{
    public class BuildOptions : IBuildOptions
    {
        public bool IncludeProperties { get; set; } = true;
        public bool IncludeFields { get; set; } = false;
    }
}
