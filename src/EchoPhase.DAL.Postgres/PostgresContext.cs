// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using System.Reflection;
using EchoPhase.DAL.Abstractions;
using EchoPhase.DAL.Postgres.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace EchoPhase.DAL.Postgres
{
    public class PostgresContext : IdentityDbContext<User, UserRole, Guid>
    {
        public const string DefaultSchema = "EchoPhase";

        /// <summary>
        /// Npgsql settings shared by the application and the EF tools
        /// (<see cref="PostgresContextFactory"/>), so migrations made or scripted
        /// at design time match the ones the application applies.
        /// </summary>
        public static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder options)
        {
            options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            // Explicitly in public: unqualified, it would follow search_path.
            options.MigrationsHistoryTable("__EFMigrationsHistory", "public");
        }

        public PostgresContext(DbContextOptions<PostgresContext> options) : base(options) { }

        public override DbSet<User> Users { get; set; } = default!;
        public DbSet<Connection> Connections { get; set; } = default!;
        public DbSet<OAuthApp> OAuthApps { get; set; } = default!;
        public DbSet<Session> Sessions { get; set; } = default!;
        public DbSet<ApiClient> ApiClients { get; set; } = default!;
        public DbSet<Impersonation> Impersonations { get; set; } = default!;
        public DbSet<ApiClientPermissionEntry> ApiClientPermissionEntries { get; set; } = default!;
        public DbSet<WebHook> WebHooks { get; set; } = default!;
        public DbSet<SessionAudit> SessionAudits { get; set; } = default!;
        public DbSet<UserPermissionGrant> UserPermissionGrants { get; set; } = default!;
        public DbSet<Workflow> Workflows { get; set; } = default!;
        public DbSet<WorkflowSchedule> WorkflowSchedules { get; set; } = default!;
        public DbSet<WorkflowVersion> WorkflowVersions { get; set; } = default!;
        public DbSet<StorageEntry> StorageEntries { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.HasDefaultSchema(DefaultSchema);
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (!typeof(ITrackingEntity).IsAssignableFrom(entityType.ClrType))
                    continue;

                builder.Entity(entityType.ClrType)
                    .Property(nameof(ITrackingEntity.UpdatedAt))
                    .IsRequired()
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                builder.Entity(entityType.ClrType)
                    .Property(nameof(ITrackingEntity.CreatedAt))
                    .IsRequired()
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            }

            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (!typeof(IConcurrentEntity).IsAssignableFrom(entityType.ClrType))
                    continue;

                builder.Entity(entityType.ClrType)
                    .Property(nameof(IConcurrentEntity.ConcurrencyStamp))
                    .IsRequired();
            }

            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                    continue;

                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var filter = Expression.Lambda(
                    Expression.Equal(
                        Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)),
                        Expression.Constant(false)),
                    parameter);

                builder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }
}
