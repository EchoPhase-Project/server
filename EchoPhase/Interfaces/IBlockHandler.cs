namespace EchoPhase.Interfaces
{
    public interface IBlockHandler
    {
        /// <summary>
        /// The type of parameters this handler processes.
        /// </summary>
        Type ParamType
        {
            get;
        }

        /// <summary>
        /// Executes the block handling with given parameters.
        /// Returns a list of IDs of the next blocks to execute.
        /// </summary>
        /// <param name="context">The execution context for the block.</param>
        /// <param name="block">Current block.</param>
        /// <param name="param">The block parameters as an object (will be deserialized inside the handler).</param>
        /// <returns>A list of IDs for the next blocks to run.</returns>
        Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, object param);
    }

    public interface IBlockHandler<TParams> : IBlockHandler
    {
        /// <summary>
        /// Executes the block handling with given parameters.
        /// Returns a list of IDs of the next blocks to execute.
        /// </summary>
        /// <param name="context">The execution context for the block.</param>
        /// <param name="block">Current block.</param>
        /// <param name="param">The block parameters as an object (will be deserialized inside the handler).</param>
        /// <returns>A list of IDs for the next blocks to run.</returns>
        Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, TParams param);
    }
}
