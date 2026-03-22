// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class ExternalTokenConfiguration : IEntityTypeConfiguration<ExternalToken>
    {
        public void Configure(EntityTypeBuilder<ExternalToken> builder)
        {
            builder.ToTable("ExternalTokens", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new { e.UserId, e.ProviderName, e.TokenName }).IsUnique();

            builder.HasOne(e => e.User)
                .WithMany(u => u.ExternalTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
