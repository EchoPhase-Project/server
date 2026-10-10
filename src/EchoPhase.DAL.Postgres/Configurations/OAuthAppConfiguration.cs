// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class OAuthAppConfiguration : IEntityTypeConfiguration<OAuthApp>
    {
        public void Configure(EntityTypeBuilder<OAuthApp> builder)
        {
            builder.ToTable("OAuthApps", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new { e.UserId, e.Provider, e.ClientId }).IsUnique();

            builder.Property(e => e.Provider).HasMaxLength(32).IsRequired();
            builder.Property(e => e.Name).HasMaxLength(64).IsRequired();
            builder.Property(e => e.ClientId).HasMaxLength(128).IsRequired();

            builder.HasOne(e => e.User)
                .WithMany(u => u.OAuthApps)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
