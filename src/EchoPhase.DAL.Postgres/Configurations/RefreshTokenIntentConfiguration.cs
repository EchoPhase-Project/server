// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class RefreshTokenIntentConfiguration : IEntityTypeConfiguration<RefreshTokenIntent>
    {
        public void Configure(EntityTypeBuilder<RefreshTokenIntent> builder)
        {
            builder.ToTable("RefreshTokenIntents", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new { e.RefreshTokenId, e.Value }).IsUnique();
        }
    }
}
