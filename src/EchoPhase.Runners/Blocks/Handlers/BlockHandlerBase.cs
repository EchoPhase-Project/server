// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System.Text.Json.Serialization;
using EchoPhase.Runners.Blocks.Contexts;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Blocks.Handlers
{
    public abstract class BlockHandlerBase<TParam> : IBlockHandler
        where TParam : class
    {
        public Type ParamType => typeof(TParam);

        protected T GetService<T>(IBlockExecutionContext context) where T : notnull
            => context.Services.GetRequiredService<T>();

        public object Bind(object? param) => BlockParamsBinder.Bind(typeof(TParam), param);

        public Task<BlockResult> ExecuteAsync(BlockCall call, object param)
            => ExecuteAsync(call, (TParam)param);

        public abstract Task<BlockResult> ExecuteAsync(BlockCall call, TParam param);


        /// <summary>
        /// Converts an API DTO into plain lists/dictionaries so scripts can address
        /// its members by their JSON names.
        /// </summary>
        protected static object? ToVariable(object? obj)
        {
            if (obj is null)
                return null;

            var type = obj.GetType();

            if (type.IsPrimitive || obj is string || obj is decimal)
                return obj;

            if (obj is System.Collections.IEnumerable enumerable)
                return enumerable
                    .Cast<object?>()
                    .Select(ToVariable)
                    .ToList();

            if (type.IsClass)
                return type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .ToDictionary(
                        prop => prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name,
                        prop => ToVariable(prop.GetValue(obj))
                    );

            return obj;
        }
    }
}
