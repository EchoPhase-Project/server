// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("RefreshTokens", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            builder.HasIndex(t => new { t.RefreshValue, t.UserId }).IsUnique();

            builder.HasOne(t => t.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(t => t.Audits)
                .WithOne(a => a.RefreshToken)
                .HasForeignKey(a => a.RefreshTokenId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(t => t.Scopes)
                .WithOne(s => s.RefreshToken)
                .HasForeignKey(s => s.RefreshTokenId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(t => t.Intents)
                .WithOne(i => i.RefreshToken)
                .HasForeignKey(i => i.RefreshTokenId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(t => t.Permissions)
                .WithOne(p => p.RefreshToken)
                .HasForeignKey(p => p.RefreshTokenId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
