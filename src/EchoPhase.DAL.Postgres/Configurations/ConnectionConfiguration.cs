// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class ConnectionConfiguration : IEntityTypeConfiguration<Connection>
    {
        public void Configure(EntityTypeBuilder<Connection> builder)
        {
            builder.ToTable("Connections", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new { e.UserId, e.Provider, e.Name }).IsUnique();

            builder.Property(e => e.Provider).HasMaxLength(32).IsRequired();
            builder.Property(e => e.Name).HasMaxLength(64).IsRequired();
            builder.Property(e => e.AuthType).HasConversion<string>().HasMaxLength(16);
            builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
            builder.Property(e => e.LastError).HasMaxLength(512);

            builder.HasOne(e => e.User)
                .WithMany(u => u.Connections)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // An app still in use cannot be removed out from under its connections.
            builder.HasOne(e => e.OAuthApp)
                .WithMany(a => a.Connections)
                .HasForeignKey(e => e.OAuthAppId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
