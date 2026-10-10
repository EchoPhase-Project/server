// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Projection.Options
{
    /// <summary>
    /// Options controlling projection behaviour.
    /// </summary>
    public class ProjectionOptions
    {
        /// <summary>
        /// When true, only properties marked with <see cref="EchoPhase.Projection.Attributes.ExposeAttribute"/>
        /// are included, at every level. Include() adds fields to them; Only() replaces them.
        /// </summary>
        public bool IncludeOnlyExpose { get; set; } = true;

        /// <summary>
        /// When true, an included object or collection keeps its content (by the [Expose]
        /// rule) unless deeper paths narrow it; when false, it is empty without them.
        /// </summary>
        public bool IncludeSubPaths { get; set; } = true;

        /// <summary>
        /// When true, the result is returned as <see cref="System.Dynamic.ExpandoObject"/>
        /// instead of <see cref="Dictionary{String, Object}"/>.
        /// </summary>
        public bool UseExpando { get; set; } = false;

        /// <summary>
        /// When true, an included property keeps its whole content (by the [Expose] rule)
        /// even when deeper paths of it are listed too.
        /// </summary>
        public bool IncludeNestedContent { get; set; } = false;

        /// <summary>
        /// Creates a shallow copy of the current options.
        /// Used by the builder so per-call overrides don't affect the projector's defaults.
        /// </summary>
        public ProjectionOptions Clone() => new()
        {
            IncludeOnlyExpose = IncludeOnlyExpose,
            IncludeSubPaths = IncludeSubPaths,
            UseExpando = UseExpando,
            IncludeNestedContent = IncludeNestedContent,
        };
    }
}
