// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Database;
using EchoPhase.DAL.Scylla.Builders;
using EchoPhase.DAL.Scylla.Database;
using Microsoft.Extensions.Options;

namespace EchoPhase.DAL.Scylla
{
    public class ScyllaContext : DbContext
    {
        public ScyllaContext(IOptions<DatabaseOptions> options) : base(options.Value.Scylla)
        {
        }

        protected override void OnModelBuilding(ModelBuilder builder)
        {
            base.OnModelBuilding(builder);
        }
    }
}
