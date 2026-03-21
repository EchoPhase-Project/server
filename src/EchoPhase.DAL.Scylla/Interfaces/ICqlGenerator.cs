// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.DAL.Scylla.Interfaces
{
    public interface ICqlGenerator
    {
        (string cql, object[] parameters) GenerateInsert(object entity, IEntityBuilder builder);
        (string cql, object[] parameters) GenerateUpdate(object entity, IEntityBuilder builder);
        (string cql, object[] parameters) GenerateDelete(object entity, IEntityBuilder builder);
    }
}
