// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class ImpersonationConfiguration : IEntityTypeConfiguration<Impersonation>
    {
        public void Configure(EntityTypeBuilder<Impersonation> builder)
        {
            builder.ToTable("Impersonations", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => e.ActorId);
            builder.HasIndex(e => e.TargetId);

            builder.Property(e => e.Reason).HasMaxLength(256).IsRequired();
            builder.Property(e => e.ActorSecurityStamp).IsRequired();

            builder.HasOne(e => e.Actor)
                .WithMany()
                .HasForeignKey(e => e.ActorId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Target)
                .WithMany()
                .HasForeignKey(e => e.TargetId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
