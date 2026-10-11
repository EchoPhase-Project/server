// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EchoPhase.DAL.Postgres
{
    /// <summary>
    /// The context for the EF tools (dotnet ef migrations add/script/...). Building
    /// migrations needs no database; for commands that connect (database update,
    /// migrations list), set ECHOPHASE_POSTGRES_CONNECTION or pass --connection.
    /// </summary>
    public class PostgresContextFactory : IDesignTimeDbContextFactory<PostgresContext>
    {
        public const string ConnectionVariable = "ECHOPHASE_POSTGRES_CONNECTION";

        public PostgresContext CreateDbContext(string[] args)
        {
            var connection = Environment.GetEnvironmentVariable(ConnectionVariable)
                ?? "Host=localhost;Database=echophase;Username=echophase";
            var options = new DbContextOptionsBuilder<PostgresContext>()
                .UseNpgsql(connection, PostgresContext.ConfigureNpgsql)
                .Options;
            return new PostgresContext(options);
        }
    }
}
