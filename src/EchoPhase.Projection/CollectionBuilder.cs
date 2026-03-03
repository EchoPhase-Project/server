using EchoPhase.Projection.Options;

namespace EchoPhase.Projection
{
    /// <summary>
    /// Fluent builder for projecting a collection of objects,
    /// applying the same projection configuration to each element.
    /// </summary>
    /// <typeparam name="T">The type of the collection elements.</typeparam>
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

        /// <summary>
        /// Configures how each element in the collection should be projected.
        /// </summary>
        /// <param name="configure">
        /// An action that receives a <see cref="ProjectionBuilder{T}"/> per element.
        /// Use <c>Include</c> and <c>WithOptions</c> inside to configure the projection.
        /// </param>
        public CollectionProjectionBuilder<T> Configure(Action<ProjectionBuilder<T>> configure)
        {
            _configure = configure;
            return this;
        }

        /// <summary>
        /// Executes the projection for each element and returns the results as a list.
        /// </summary>
        public List<object?> Build()
        {
            var projector = new Projector(_options);

            return _source.Select(item =>
            {
                // Каждый элемент получает свой изолированный билдер с клоном опций
                var builder = projector.For(item);
                _configure?.Invoke(builder);
                return builder.Build();
            }).ToList();
        }

        /// <summary>
        /// Executes the projection and returns the results as a list of dictionaries.
        /// </summary>
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
