// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using EchoPhase.Projection.Options;

namespace EchoPhase.Projection
{
    /// <summary>
    /// Fluent builder for configuring and executing a projection.
    /// </summary>
    public class ProjectionBuilder<T> : IProjectionBuilder
    {
        private readonly T _source;
        private readonly ProjectionOptions _options;
        private readonly List<Expression<Func<T, object?>>> _fields = new();
        private bool _exclusive;

        /// <summary>
        /// Per-property collection configs keyed by property name.
        /// Values implement <see cref="ICollectionConfig"/> so Projector can work
        /// with them without knowing the element type at compile time.
        /// </summary>
        internal readonly Dictionary<string, ICollectionConfig> _collectionConfigs = new();

        internal ProjectionBuilder(T source, ProjectionOptions options)
        {
            _source = source;
            _options = options;
        }

        /// <summary>
        /// Adds top-level or nested properties (<c>x =&gt; x.Address.City</c>). Under
        /// <see cref="ProjectionOptions.IncludeOnlyExpose"/> they come in addition to
        /// the [Expose] ones, so this can reveal a property that is not exposed.
        /// </summary>
        public ProjectionBuilder<T> Include(params Expression<Func<T, object?>>[] fields)
        {
            _fields.AddRange(fields);
            return this;
        }

        /// <summary>
        /// Just these properties: no [Expose] ones are added next to them. The
        /// content of an included object still follows the [Expose] rule.
        /// </summary>
        public ProjectionBuilder<T> Only(params Expression<Func<T, object?>>[] fields)
        {
            _exclusive = true;
            _fields.AddRange(fields);
            return this;
        }

        /// <summary>
        /// Includes a collection property and configures how each element is projected.
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

            _collectionConfigs[name] = new CollectionConfig<TItem>(
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

        public ProjectionBuilder<T> WithNestedContent()
        {
            _options.IncludeNestedContent = true;
            return this;
        }

        /// <summary>
        /// Re-interprets the source as <typeparamref name="TAs"/> for [Expose] resolution
        /// and Include expressions. Call it first: fields and collection configs set
        /// before it belong to <typeparamref name="T"/> and are not carried over.
        /// </summary>
        public ProjectionBuilder<TAs> As<TAs>() where TAs : class
        {
            if (_source is not TAs typed)
                throw new InvalidCastException(
                    $"Source of type '{typeof(T).Name}' cannot be cast to '{typeof(TAs).Name}'.");

            if (_fields.Count > 0 || _collectionConfigs.Count > 0)
                throw new InvalidOperationException("As<TAs>() must come before Include, Only and IncludeCollection.");

            return new ProjectionBuilder<TAs>(typed, _options);
        }

        /// <summary>Executes the projection and returns the result.</summary>
        public object? Build()
        {
            var projector = new Projector(_options);
            return projector.Execute(_source, _options, _fields, _collectionConfigs, _exclusive);
        }

        /// <summary>Executes and returns <see cref="Dictionary{String, Object}"/>.</summary>
        public Dictionary<string, object?> BuildDictionary()
        {
            _options.UseExpando = false;
            return Build() switch
            {
                Dictionary<string, object?> dictionary => dictionary,
                null => throw new InvalidOperationException("There is nothing to project: the source is null."),
                var other => throw new InvalidOperationException($"The projection is a {other.GetType().Name}, not a dictionary."),
            };
        }

        /// <summary>Executes and returns <see cref="System.Dynamic.ExpandoObject"/>.</summary>
        public dynamic BuildExpando()
        {
            _options.UseExpando = true;
            return Build() ?? throw new InvalidOperationException("There is nothing to project: the source is null.");
        }
    }
}
