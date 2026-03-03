namespace EchoPhase.Projection.Options
{
    /// <summary>
    /// Options controlling projection behaviour.
    /// </summary>
    public class ProjectionOptions
    {
        /// <summary>
        /// When true, only properties marked with <see cref="EchoPhase.Projection.Attributes.ExposeAttribute"/>
        /// are included (unless explicit fields are provided via Include()).
        /// </summary>
        public bool IncludeOnlyExpose { get; set; } = true;

        /// <summary>
        /// When true, if a parent path is included, all its children are included automatically.
        /// </summary>
        public bool IncludeSubPaths { get; set; } = true;

        /// <summary>
        /// When true, the result is returned as <see cref="System.Dynamic.ExpandoObject"/>
        /// instead of <see cref="Dictionary{String, Object}"/>.
        /// </summary>
        public bool UseExpando { get; set; } = false;

        /// <summary>
        /// Creates a shallow copy of the current options.
        /// Used by the builder so per-call overrides don't affect the projector's defaults.
        /// </summary>
        public ProjectionOptions Clone() => new()
        {
            IncludeOnlyExpose = IncludeOnlyExpose,
            IncludeSubPaths   = IncludeSubPaths,
            UseExpando        = UseExpando,
        };
    }
}
