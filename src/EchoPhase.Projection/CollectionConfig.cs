// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Projection
{
    /// <summary>
    /// Holds configuration for projecting elements of a specific collection property.
    /// </summary>
    internal sealed class CollectionConfig
    {
        public Type ItemType
        {
            get;
        }

        /// <summary>
        /// Creates a configured ProjectionBuilder for a single collection element.
        /// </summary>
        public Func<Projector, object, IProjectionBuilder> Apply
        {
            get;
        }

        public CollectionConfig(Type itemType, Func<Projector, object, IProjectionBuilder> apply)
        {
            ItemType = itemType;
            Apply = apply;
        }
    }
}
