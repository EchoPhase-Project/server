// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Projection.Options;

namespace EchoPhase.Projection
{
    /// <summary>
    /// Fluent builder for projecting a collection of objects.
    /// Internally uses <see cref="ICollectionConfig"/> so the per-element
    /// projection is typed at the call site but stored without boxing.
    /// </summary>
    public class CollectionProjectionBuilder<T>
    {
        private readonly IEnumerable<T> _source;
        private readonly ProjectionOptions _options;
        private Action<ProjectionBuilder<T>>? _configure;

        internal CollectionProjectionBuilder(IEnumerable<T> source, ProjectionOptions options)
        {
            _source = source;
            _options = options;
        }

        public CollectionProjectionBuilder<T> Configure(Action<ProjectionBuilder<T>> configure)
        {
            _configure = configure;
            return this;
        }

        public List<object?> Build()
        {
            var projector = new Projector(_options);
            return _source.Select(item =>
            {
                var builder = projector.For(item);
                _configure?.Invoke(builder);
                return builder.Build();
            }).ToList();
        }

        public List<Dictionary<string, object?>> BuildDictionaries()
        {
            var projector = new Projector(_options);
            return _source.Select(item =>
            {
                var builder = projector.For(item);
                _configure?.Invoke(builder);
                return builder.BuildDictionary();
            }).ToList();
        }
    }
}
