// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Storage
{
    /// <summary>Whose values: one workflow's own, or shared by the workflows of its package.</summary>
    public enum StorageScope
    {
        Workflow,
        Package
    }

    /// <summary>
    /// Where a value lives: a user's package, in one of its workflows
    /// (WorkflowId) or shared by them (null). The user is whose quota it counts against.
    /// </summary>
    public readonly record struct StorageSlot(Guid UserId, Guid PackageId, Guid? WorkflowId);

    /// <summary>A stored value; Value is null in listings without values.</summary>
    public sealed record StorageItem(string Key, object? Value, int Size, DateTime UpdatedAt, DateTime? ExpiresAt);

    /// <summary>A write the user's storage has no room for, or a value too big.</summary>
    public sealed class StorageLimitException(string message) : Exception(message);

    /// <summary>
    /// Values workflows keep between runs (the Storage block); the host
    /// provides it. Values are the language's (lists, objects, numbers...),
    /// stored as JSON. Expired values read as missing.
    /// </summary>
    public interface IWorkflowStore
    {
        Task<StorageItem?> GetAsync(StorageSlot slot, string key, CancellationToken ct);

        /// <summary>Writes the value; ttl null: it does not expire.</summary>
        Task SetAsync(StorageSlot slot, string key, object? value, TimeSpan? ttl, CancellationToken ct);

        /// <summary>
        /// Replaces the value with update(current) (current null when there is
        /// none) as one step: concurrent runs wait for each other, so none
        /// loses another's change. ttl null keeps the expiry it had.
        /// </summary>
        Task<object?> UpdateAsync(StorageSlot slot, string key, Func<object?, object?> update, TimeSpan? ttl, CancellationToken ct);

        /// <summary>True when there was a value.</summary>
        Task<bool> DeleteAsync(StorageSlot slot, string key, CancellationToken ct);

        /// <summary>Keys in order, starting with prefix, after the key `after`.</summary>
        Task<IReadOnlyList<StorageItem>> ListAsync(
            StorageSlot slot, string? prefix, string? after, int limit, bool values, CancellationToken ct);
    }
}
