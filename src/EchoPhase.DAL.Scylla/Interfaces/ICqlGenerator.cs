// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla.Interfaces
{
    public interface ICqlGenerator
    {
        /// <summary>
        /// Every statement needed to persist a tracked change: the insert, update
        /// or delete itself, plus a column delete for <see cref="TrackedEntity.ClearedColumns"/>.
        /// </summary>
        IReadOnlyList<(string cql, object?[] parameters)> Generate(TrackedEntity tracked, IEntityBuilder builder);

        (string cql, object?[] parameters) GenerateInsert(object entity, IEntityBuilder builder, WriteOptions? options = null);
        (string cql, object?[] parameters) GenerateUpdate(object entity, IEntityBuilder builder, WriteOptions? options = null);
        (string cql, object?[] parameters) GenerateDelete(object entity, IEntityBuilder builder, WriteOptions? options = null);

        (string cql, object?[] parameters) GenerateClearColumns(
            object entity, IEntityBuilder builder, IReadOnlyCollection<string> properties, WriteOptions? options = null);
    }
}
