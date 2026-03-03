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

        /// <summary>
        /// Per-property collection configs. Key = property name, Value = Action&lt;ProjectionBuilder&lt;TItem&gt;&gt;.
        /// Stored as Delegate because TItem varies per property.
        /// </summary>
        internal readonly Dictionary<string, CollectionConfig> CollectionConfigs = new();

        internal ProjectionBuilder(T source, ProjectionOptions options)
        {
            _source = source;
            _options = options;
        }

        /// <summary>
        /// Specifies which top-level or nested properties to include.
        /// </summary>
        public ProjectionBuilder<T> Include(params Expression<Func<T, object?>>[] fields)
        {
            _fields.AddRange(fields);
            return this;
        }

        /// <summary>
        /// Includes a collection property and configures how each element is projected.
        /// </summary>
        /// <typeparam name="TItem">The element type of the collection.</typeparam>
        /// <param name="collection">Expression selecting the collection property.</param>
        /// <param name="configure">Action that configures projection for each element.</param>
        public ProjectionBuilder<T> IncludeCollection<TItem>(
            Expression<Func<T, IEnumerable<TItem>?>> collection,
            Action<ProjectionBuilder<TItem>> configure)
        {
            // Wrap to object? so ExtractMemberPath can handle it uniformly
            var wrapped = Expression.Lambda<Func<T, object?>>(
                Expression.Convert(collection.Body, typeof(object)),
                collection.Parameters);

            var name = Projector.ExtractMemberPath(wrapped);

            // Register the property path so FillDict includes it
            _fields.Add(wrapped);

            // Store config alongside a factory that creates a typed builder for each element
            CollectionConfigs[name] = new CollectionConfig(
                itemType: typeof(TItem),
                apply: (projector, item) =>
                {
                    var builder = new ProjectionBuilder<TItem>((TItem)item, projector._defaultOptions.Clone());
                    configure(builder);
                    return builder;
                }
            );

            return this;
        }

        /// <summary>
        /// Overrides projection options for this call only.
        /// </summary>
        public ProjectionBuilder<T> WithOptions(Action<ProjectionOptions> configure)
        {
            configure(_options);
            return this;
        }

        /// <summary>
        /// Reinterprets the source as <typeparamref name="TAs"/> for [Expose] resolution and Include expressions.
        /// The source must be assignable to <typeparamref name="TAs"/>.
        /// </summary>
        public ProjectionBuilder<TAs> As<TAs>() where TAs : class
        {
            if (_source is not TAs typed)
                throw new InvalidCastException(
                    $"Source of type '{typeof(T).Name}' cannot be cast to '{typeof(TAs).Name}'.");

            return new ProjectionBuilder<TAs>(typed, _options);
        }

        /// <summary>
        /// Executes the projection and returns the result as a dictionary or ExpandoObject.
        /// </summary>
        public object? Build()
        {
            var projector = new Projector(_options);
            return projector.Execute(_source, _options, _fields, CollectionConfigs);
        }

        /// <summary>
        /// Executes the projection and returns <see cref="Dictionary{String, Object}"/>.
        /// </summary>
        public Dictionary<string, object?> BuildDictionary()
        {
            _options.UseExpando = false;
            return (Dictionary<string, object?>)Build()!;
        }

        /// <summary>
        /// Executes the projection and returns <see cref="System.Dynamic.ExpandoObject"/>.
        /// </summary>
        public dynamic BuildExpando()
        {
            _options.UseExpando = true;
            return Build()!;
        }
    }
}
