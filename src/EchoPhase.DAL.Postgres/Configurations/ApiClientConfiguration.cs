// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class ApiClientConfiguration : IEntityTypeConfiguration<ApiClient>
    {
        public void Configure(EntityTypeBuilder<ApiClient> builder)
        {
            builder.ToTable("ApiClients", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => e.UserId);

            builder.Property(e => e.Name).HasMaxLength(64).IsRequired();
            builder.Property(e => e.SecretHash).HasMaxLength(64).IsRequired();
            builder.Property(e => e.PreviousSecretHash).HasMaxLength(64);

            builder.HasOne(e => e.User)
                .WithMany(u => u.ApiClients)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.Permissions)
                .WithOne(p => p.ApiClient)
                .HasForeignKey(p => p.ApiClientId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }

    public class ApiClientPermissionEntryConfiguration : IEntityTypeConfiguration<ApiClientPermissionEntry>
    {
        public void Configure(EntityTypeBuilder<ApiClientPermissionEntry> builder)
        {
            builder.ToTable("ApiClientPermissionEntries", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new { e.ApiClientId, e.Resource }).IsUnique();

            builder.Property(e => e.Permissions).HasColumnType("text[]");
        }
    }
}
