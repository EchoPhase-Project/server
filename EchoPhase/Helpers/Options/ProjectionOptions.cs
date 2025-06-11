namespace EchoPhase.Helpers.Options
{
    public class ProjectionOptions
    {
        public bool UseExpando { get; set; } = false;
        public bool IncludeSubPaths { get; set; } = true;
        public bool IncludeOnlyExpose { get; set; } = false;
        public int? MaxDepth { get; set; } = null;
    }
}
