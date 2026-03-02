namespace EchoPhase.Projection.Options
{
    public class ProjectionOptions
    {
        public bool UseExpando { get; set; } = false;
        public bool IncludeSubPaths { get; set; } = true;
        public bool IncludeOnlyExpose { get; set; } = true;
        public int? MaxDepth { get; set; } = null;
    }
}
