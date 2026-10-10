// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class UserPermissionGrantConfiguration : IEntityTypeConfiguration<UserPermissionGrant>
    {
        public void Configure(EntityTypeBuilder<UserPermissionGrant> builder)
        {
            builder.ToTable("UserPermissionGrants", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new { e.UserId, e.Resource }).IsUnique();

            // The expiry sweep only looks at grants whose expiry is still pending.
            builder.HasIndex(e => e.ExpiresAt)
                .HasFilter("\"ExpiresAt\" IS NOT NULL AND NOT \"ExpiryApplied\"");

            builder.Property(e => e.Resource).HasMaxLength(64).IsRequired();
            builder.Property(e => e.Permissions).HasColumnType("text[]");
            builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();

            builder.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.GrantedBy)
                .WithMany()
                .HasForeignKey(e => e.GrantedById)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
