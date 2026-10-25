// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using System.Reflection;
using EchoPhase.DAL.Abstractions;
using EchoPhase.DAL.Postgres.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using UUIDNext;

namespace EchoPhase.DAL.Postgres
{
    public class PostgresContext : IdentityDbContext<User, UserRole, Guid>
    {
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            RenewConcurrencyStamps();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            RenewConcurrencyStamps();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>
        /// A new stamp for every entity being written; the one it was read with
        /// stays the original value, which the UPDATE checks (a concurrency token).
        /// Here rather than in an interceptor, so every context does it, tests' too.
        /// </summary>
        private void RenewConcurrencyStamps()
        {
            foreach (var entry in ChangeTracker.Entries<IConcurrentEntity>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                    entry.Entity.ConcurrencyStamp = Uuid.NewDatabaseFriendly(UUIDNext.Database.PostgreSql);
            }
        }

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

        public DbSet<Connection> Connections => Set<Connection>();
        public DbSet<OAuthApp> OAuthApps => Set<OAuthApp>();
        public DbSet<Session> Sessions => Set<Session>();
        public DbSet<ApiClient> ApiClients => Set<ApiClient>();
        public DbSet<Impersonation> Impersonations => Set<Impersonation>();
        public DbSet<ApiClientPermissionEntry> ApiClientPermissionEntries => Set<ApiClientPermissionEntry>();
        public DbSet<WebHook> WebHooks => Set<WebHook>();
        public DbSet<SessionAudit> SessionAudits => Set<SessionAudit>();
        public DbSet<UserPermissionGrant> UserPermissionGrants => Set<UserPermissionGrant>();
        public DbSet<Workflow> Workflows => Set<Workflow>();
        public DbSet<WorkflowSchedule> WorkflowSchedules => Set<WorkflowSchedule>();
        public DbSet<WorkflowVersion> WorkflowVersions => Set<WorkflowVersion>();
        public DbSet<StorageEntry> StorageEntries => Set<StorageEntry>();
        public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<NoteShare> NoteShares => Set<NoteShare>();
        public DbSet<WorkflowPackage> WorkflowPackages => Set<WorkflowPackage>();
        public DbSet<WorkflowPackageVersion> WorkflowPackageVersions => Set<WorkflowPackageVersion>();
        public DbSet<WorkflowPackageShare> WorkflowPackageShares => Set<WorkflowPackageShare>();
        public DbSet<WorkflowPackageInstall> WorkflowPackageInstalls => Set<WorkflowPackageInstall>();

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

                // Every save writes a new stamp and checks the one it read: a
                // concurrent change makes the later save fail instead of
                // silently overwriting the earlier one.
                builder.Entity(entityType.ClrType)
                    .Property(nameof(IConcurrentEntity.ConcurrencyStamp))
                    .IsRequired()
                    .IsConcurrencyToken();
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
