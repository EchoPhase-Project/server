// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Runners.Blocks.Storage;
using EchoPhase.Scripting;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Blocks.Handlers
{
    /// <summary>
    /// Values kept between runs: a counter, the ids already seen (a Bloom
    /// filter), a cursor into someone's API. Update computes the new value
    /// from the current one atomically, so parallel runs lose nothing.
    /// </summary>
    [BlockType(BlockTypes.Storage,
        Description = "Keeps values between runs: get, set, update (from the current value, atomically), delete or list keys.")]
    public class StorageHandler : BlockHandlerBase<StorageParams>
    {
        public const string CurrentValueName = "value";

        public override async Task<BlockResult> ExecuteAsync(BlockCall call, StorageParams param)
        {
            var context = call.Context;
            var store = context.Services.GetService<IWorkflowStore>()
                ?? throw new InvalidOperationException("Storage is not available here.");
            var slot = Slot(context, param.Scope);
            var ct = context.CancellationToken;
            TimeSpan? ttl = param.TtlSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null;

            switch (param.Operation)
            {
                case StorageOperation.Get:
                    var item = await store.GetAsync(slot, Key(param, call), ct);
                    return BlockResult.Main(item is not null ? item.Value : Evaluate(param.Default, call.Scope));

                case StorageOperation.Set:
                    var value = Evaluate(param.Value, call.Scope);
                    await store.SetAsync(slot, Key(param, call), value, ttl, ct);
                    return BlockResult.Main(value);

                case StorageOperation.Update:
                    var updated = await store.UpdateAsync(slot, Key(param, call),
                        current => Evaluate(param.Value, Eval.With(call.Scope, CurrentValueName, current)), ttl, ct);
                    return BlockResult.Main(updated);

                case StorageOperation.Delete:
                    return BlockResult.Main(await store.DeleteAsync(slot, Key(param, call), ct));

                case StorageOperation.List:
                    var items = await store.ListAsync(slot, Template(param.Prefix, call), Template(param.After, call),
                        param.Limit, param.Values, ct);
                    return BlockResult.Main(items.Select(i => List(i, param.Values)).ToList());

                default:
                    throw new InvalidOperationException($"Unknown storage operation {param.Operation}.");
            }
        }

        private static StorageSlot Slot(IBlockExecutionContext context, StorageScope scope)
        {
            if (context.PackageId is not { } package || context.WorkflowId is not { } workflow)
                throw new InvalidOperationException("Storage belongs to a saved workflow and its package; this program runs unsaved.");
            return new StorageSlot(context.UserId, package, scope == StorageScope.Package ? null : workflow);
        }

        private static string Key(StorageParams param, BlockCall call)
        {
            var key = Eval.TypedProcess<string>(param.Key, call.Scope).Trim();
            return key.Length > 0 ? key : throw new InvalidOperationException("The key came out empty.");
        }

        private static string? Template(string? template, BlockCall call)
            => string.IsNullOrEmpty(template) ? null : Eval.TypedProcess<string>(template, call.Scope);

        private static object? Evaluate(JsonElement value, IDictionary<string, object?> scope)
            => value.ValueKind == JsonValueKind.Undefined ? null : BlockValues.Evaluate(value, scope);

        private static Dictionary<string, object?> List(StorageItem item, bool withValue)
        {
            var entry = new Dictionary<string, object?>
            {
                ["key"] = item.Key,
                ["size"] = item.Size,
                ["updatedAt"] = item.UpdatedAt,
                ["expiresAt"] = item.ExpiresAt,
            };
            if (withValue)
                entry["value"] = item.Value;
            return entry;
        }
    }
}
