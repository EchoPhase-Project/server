// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class SessionAuditConfiguration : IEntityTypeConfiguration<SessionAudit>
    {
        public void Configure(EntityTypeBuilder<SessionAudit> builder)
        {
            builder.ToTable("SessionAudits", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => e.SessionId);
            builder.HasIndex(e => e.TokenHash).IsUnique();

            builder.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
        }
    }
}
