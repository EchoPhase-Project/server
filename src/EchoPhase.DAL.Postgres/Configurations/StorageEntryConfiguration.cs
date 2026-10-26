// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class StorageEntryConfiguration : IEntityTypeConfiguration<StorageEntry>
    {
        public const int MaxKeyLength = 200;

        public void Configure(EntityTypeBuilder<StorageEntry> builder)
        {
            builder.ToTable("StorageEntries", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            // One value per key and scope; the package's shared scope (no
            // workflow) is a scope too, so nulls count as equal.
            builder.HasIndex(e => new { e.PackageId, e.WorkflowId, e.Key })
                .IsUnique()
                .AreNullsDistinct(false);
            // prune: what has expired.
            builder.HasIndex(e => e.ExpiresAt);
            // The user's quota.
            builder.HasIndex(e => e.UserId);

            builder.Property(e => e.Key).HasMaxLength(MaxKeyLength).IsRequired();
            builder.Property(e => e.Value).HasColumnType("jsonb").IsRequired();

            builder.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Package)
                .WithMany()
                .HasForeignKey(e => e.PackageId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Workflow)
                .WithMany()
                .HasForeignKey(e => e.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
