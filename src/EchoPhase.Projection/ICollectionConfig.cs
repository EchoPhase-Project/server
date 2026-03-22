// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Projection
{
    /// <summary>
    /// Non-generic marker so <see cref="Projector"/> can store configs in a plain dictionary
    /// without knowing <typeparamref name="TItem"/> at the storage site.
    /// </summary>
    internal interface ICollectionConfig
    {
        Type ItemType { get; }

        /// <summary>
        /// Creates a configured <see cref="IProjectionBuilder"/> for a single element.
        /// The element is passed as <c>object</c> only at this boundary; the generic
        /// implementation below casts it safely.
        /// </summary>
        IProjectionBuilder ApplyBoxed(Projector projector, object item);
    }

    /// <summary>
    /// Strongly-typed collection projection config.
    /// Replaces the previous <c>CollectionConfig</c> class that used <c>object</c>
    /// for both the item type and the apply delegate, forcing an unsafe cast in Projector.
    /// </summary>
    internal sealed class CollectionConfig<TItem> : ICollectionConfig
    {
        public Type ItemType => typeof(TItem);

        private readonly Func<Projector, TItem, IProjectionBuilder> _apply;

        public CollectionConfig(Func<Projector, TItem, IProjectionBuilder> apply)
        {
            _apply = apply;
        }

        public IProjectionBuilder ApplyBoxed(Projector projector, object item)
            => _apply(projector, (TItem)item);

        public IProjectionBuilder Apply(Projector projector, TItem item)
            => _apply(projector, item);
    }
}
