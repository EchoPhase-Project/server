// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using EchoPhase.Projection.Options;

namespace EchoPhase.Projection
{
    /// <summary>
    /// Fluent builder for configuring and executing a projection.
    /// Collection configs are now typed via <see cref="CollectionConfig{TItem}"/>,
    /// eliminating the <c>object</c> cast that existed in the previous design.
    /// </summary>
    public class ProjectionBuilder<T> : IProjectionBuilder
    {
        private readonly T _source;
        private readonly ProjectionOptions _options;
        private readonly List<Expression<Func<T, object?>>> _fields = new();

        /// <summary>
        /// Per-property collection configs keyed by property name.
        /// Values implement <see cref="ICollectionConfig"/> so Projector can work
        /// with them without knowing the element type at compile time.
        /// </summary>
        internal readonly Dictionary<string, ICollectionConfig> CollectionConfigs = new();

        internal ProjectionBuilder(T source, ProjectionOptions options)
        {
            _source = source;
            _options = options;
        }

        /// <summary>Specifies which top-level or nested properties to include.</summary>
        public ProjectionBuilder<T> Include(params Expression<Func<T, object?>>[] fields)
        {
            _fields.AddRange(fields);
            return this;
        }

        /// <summary>
        /// Includes a collection property and configures how each element is projected.
        /// The delegate receives a strongly-typed <see cref="ProjectionBuilder{TItem}"/>
        /// — no boxing, no casting.
        /// </summary>
        public ProjectionBuilder<T> IncludeCollection<TItem>(
            Expression<Func<T, IEnumerable<TItem>?>> collection,
            Action<ProjectionBuilder<TItem>> configure)
        {
            var wrapped = Expression.Lambda<Func<T, object?>>(
                Expression.Convert(collection.Body, typeof(object)),
                collection.Parameters);

            var name = Projector.ExtractMemberPath(wrapped);

            _fields.Add(wrapped);

            // Store a typed CollectionConfig<TItem> — no object cast in Apply.
            CollectionConfigs[name] = new CollectionConfig<TItem>(
                (projector, item) =>
                {
                    var builder = new ProjectionBuilder<TItem>(item, projector._defaultOptions.Clone());
                    configure(builder);
                    return builder;
                }
            );

            return this;
        }

        /// <summary>Overrides projection options for this call only.</summary>
        public ProjectionBuilder<T> WithOptions(Action<ProjectionOptions> configure)
        {
            configure(_options);
            return this;
        }

        /// <summary>
        /// Re-interprets the source as <typeparamref name="TAs"/> for [Expose] resolution
        /// and Include expressions.
        /// </summary>
        public ProjectionBuilder<TAs> As<TAs>() where TAs : class
        {
            if (_source is not TAs typed)
                throw new InvalidCastException(
                    $"Source of type '{typeof(T).Name}' cannot be cast to '{typeof(TAs).Name}'.");

            return new ProjectionBuilder<TAs>(typed, _options);
        }

        /// <summary>Executes the projection and returns the result.</summary>
        public object? Build()
        {
            var projector = new Projector(_options);
            return projector.Execute(_source, _options, _fields, CollectionConfigs);
        }

        /// <summary>Executes and returns <see cref="Dictionary{String, Object}"/>.</summary>
        public Dictionary<string, object?> BuildDictionary()
        {
            _options.UseExpando = false;
            return (Dictionary<string, object?>)Build()!;
        }

        /// <summary>Executes and returns <see cref="System.Dynamic.ExpandoObject"/>.</summary>
        public dynamic BuildExpando()
        {
            _options.UseExpando = true;
            return Build()!;
        }
    }
}
