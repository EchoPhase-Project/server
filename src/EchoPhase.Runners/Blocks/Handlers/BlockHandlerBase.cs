using System.Text.Json;
using EchoPhase.Types.Results;
using EchoPhase.Configuration.Settings;
using Newtonsoft.Json.Linq;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Blocks.Handlers
{
    public abstract class BlockHandlerBase<TParam> : IBlockHandler<TParam>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly JsonSerializerOptions _serializerOptions = new();

        protected BlockHandlerBase(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Type ParamType => typeof(TParam);

        protected T GetService<T>() where T : notnull
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        public async Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, object param)
        {
            TParam? typedParam = default;

            if (param is null)
                throw new ArgumentNullException("Missing params exception.");

            if (param is JsonElement jsonElement)
                typedParam = jsonElement.Deserialize<TParam>(_serializerOptions);
            if (param is JObject jObject)
                typedParam = jObject.ToObject<TParam>();
            else if (param is TParam)
                typedParam = (TParam)param;

            if (typedParam is null)
                throw new InvalidCastException($"Invalid param type. Expected {typeof(TParam)}, got {param?.GetType()}.");

            await ValidateParamAsync(context, typedParam);

            return await ExecuteAsync(context, block, typedParam);
        }

        protected virtual async Task ValidateParamAsync(IBlockExecutionContext context, TParam param)
        {
            if (param is null)
                throw new ArgumentNullException("Missing param exception.");

            if (param is not IValidatable validatableParam)
                throw new InvalidOperationException("Param is not validatable.");

            var result = validatableParam.Validate();
            if (!result.Successful)
            {
                throw new InvalidOperationException(result.Error.Value);
            }

            await Task.CompletedTask;
        }

        public abstract Task<IEnumerable<int>> ExecuteAsync(IBlockExecutionContext context, IBlock block, TParam param);
    }
}
