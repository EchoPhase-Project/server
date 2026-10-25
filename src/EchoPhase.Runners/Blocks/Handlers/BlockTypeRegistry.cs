// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Blocks.Handlers
{
    /// <summary>What one block type (at one version) is: its handler, params and outputs.</summary>
    public sealed record BlockTypeDescriptor(
        BlockTypes Type,
        int Version,
        Type HandlerType,
        Type ParamsType,
        IReadOnlyList<string> Outputs,
        bool IsTrigger,
        string Description)
    {
        public string FirstOutput => Outputs.Count > 0 ? Outputs[0] : Models.BlockOutputs.Main;
    }

    /// <summary>
    /// Every block type the runner knows, found from [BlockType] handlers.
    /// The source for validating programs and for describing types to the editor.
    /// </summary>
    public sealed class BlockTypeRegistry
    {
        private readonly Dictionary<(BlockTypes, int), BlockTypeDescriptor> _types;

        public BlockTypeRegistry(IEnumerable<Type> handlerTypes)
        {
            _types = handlerTypes
                .Select(Describe)
                .ToDictionary(d => (d.Type, d.Version));
        }

        public static BlockTypeRegistry FromAssembly(Assembly assembly)
            => new(assembly.GetTypes().Where(t =>
                !t.IsAbstract && t.GetCustomAttribute<BlockTypeAttribute>() is not null));

        public IEnumerable<BlockTypeDescriptor> All
            => _types.Values.OrderBy(d => d.Type).ThenBy(d => d.Version);

        public BlockTypeDescriptor? Find(BlockTypes type, int version)
            => _types.GetValueOrDefault((type, version));

        public void RegisterHandlers(IServiceCollection services)
        {
            foreach (var descriptor in _types.Values)
                services.AddTransient(descriptor.HandlerType);
        }

        private static BlockTypeDescriptor Describe(Type handler)
        {
            var attribute = handler.GetCustomAttribute<BlockTypeAttribute>()
                ?? throw new InvalidOperationException($"{handler.Name} has no [BlockType] attribute.");
            var paramsType = FindParamsType(handler)
                ?? throw new InvalidOperationException($"{handler.Name} does not derive from BlockHandlerBase<TParams>.");
            return new BlockTypeDescriptor(attribute.Type, attribute.Version, handler, paramsType,
                attribute.Outputs, attribute.IsTrigger, attribute.Description);
        }

        private static Type? FindParamsType(Type? type)
        {
            for (; type is not null; type = type.BaseType)
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BlockHandlerBase<>))
                    return type.GetGenericArguments()[0];
            return null;
        }
    }
}
