// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class NoteConfiguration : IEntityTypeConfiguration<Note>
    {
        public const int MaxTitleLength = 200;

        /// <summary>About 100 KB of Markdown: a long document, not a file store.</summary>
        public const int MaxBodyLength = 100_000;

        public void Configure(EntityTypeBuilder<Note> builder)
        {
            builder.ToTable("Notes", PostgresContext.DefaultSchema);
            builder.HasKey(n => n.Id);
            // The owner's list: pinned first, recently changed first.
            builder.HasIndex(n => new { n.OwnerId, n.Pinned, n.UpdatedAt });
            // Reminders due: only the few notes that have one.
            builder.HasIndex(n => n.RemindAt).HasFilter("\"RemindAt\" IS NOT NULL");

            builder.Property(n => n.Title).HasMaxLength(MaxTitleLength).IsRequired();
            builder.Property(n => n.Body).HasMaxLength(MaxBodyLength).IsRequired();
            // A reminder is claimed by clearing it only where it still holds the
            // time it was read with: of several servers, one sends it.
            builder.Property(n => n.RemindAt).IsConcurrencyToken();

            builder.HasOne(n => n.Owner)
                .WithMany()
                .HasForeignKey(n => n.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class NoteShareConfiguration : IEntityTypeConfiguration<NoteShare>
    {
        public void Configure(EntityTypeBuilder<NoteShare> builder)
        {
            builder.ToTable("NoteShares", PostgresContext.DefaultSchema);
            builder.HasKey(s => new { s.NoteId, s.UserId });
            // The notes shared with a user.
            builder.HasIndex(s => s.UserId);

            builder.HasOne(s => s.Note)
                .WithMany(n => n.Shares)
                .HasForeignKey(s => s.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
