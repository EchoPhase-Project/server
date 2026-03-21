// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Configuration.Database.Redis
{
    public interface IRedisOptions
    {
        string ConnectionString
        {
            get;
        }
        string InstanceName
        {
            get;
        }
        Guid TenantId
        {
            get;
        }
    }
}
