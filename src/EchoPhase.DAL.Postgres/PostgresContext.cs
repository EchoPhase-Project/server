// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using System.Reflection;
using EchoPhase.DAL.Abstractions;
using EchoPhase.DAL.Postgres.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres
{
    public class PostgresContext : IdentityDbContext<User, UserRole, Guid>
    {
        public const string DefaultSchema = "EchoPhase";

        public PostgresContext(DbContextOptions<PostgresContext> options) : base(options) { }

        public override DbSet<User> Users { get; set; } = default!;
        public DbSet<ExternalToken> ExternalTokens { get; set; } = default!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = default!;
        public DbSet<WebHook> WebHooks { get; set; } = default!;
        public DbSet<RefreshTokenScope> RefreshTokenScopes { get; set; } = default!;
        public DbSet<RefreshTokenIntent> RefreshTokenIntents { get; set; } = default!;
        public DbSet<RefreshTokenPermissionEntry> RefreshTokenPermissionEntries { get; set; } = default!;
        public DbSet<RefreshTokenAudit> RefreshTokenAudits { get; set; } = default!;

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
