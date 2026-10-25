// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
    {
        public const int MaxNameLength = 255;

        public void Configure(EntityTypeBuilder<StoredFile> builder)
        {
            builder.ToTable("StoredFiles", PostgresContext.DefaultSchema);
            builder.HasKey(f => f.Id);
            // A user's files of one kind, newest last (ids are time-ordered).
            builder.HasIndex(f => new { f.OwnerId, f.Purpose, f.Id });
            // prune: uploads that never completed.
            builder.HasIndex(f => new { f.Status, f.CreatedAt });
            builder.HasIndex(f => f.StorageKey).IsUnique();

            builder.Property(f => f.Purpose).HasConversion<string>().HasMaxLength(32);
            builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(16);
            builder.Property(f => f.Name).HasMaxLength(MaxNameLength).IsRequired();
            builder.Property(f => f.ContentType).HasMaxLength(127).IsRequired();
            builder.Property(f => f.Sha256).HasMaxLength(64).IsRequired();
            builder.Property(f => f.StorageKey).HasMaxLength(64).IsRequired();

            builder.HasOne(f => f.Owner)
                .WithMany()
                .HasForeignKey(f => f.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
