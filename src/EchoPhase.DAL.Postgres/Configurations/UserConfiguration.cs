// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users", PostgresContext.DefaultSchema, t =>
                t.HasComment("Authorised User Model"));

            builder.HasKey(e => e.Id);
            builder.HasIndex(u => u.UserName).IsUnique();
            builder.HasIndex(u => u.NormalizedUserName)
                .IsUnique()
                .HasDatabaseName("UserNameIndex");
            builder.HasIndex(u => u.NormalizedEmail)
                .HasDatabaseName("EmailIndex");

            builder.Property(u => u.Name).HasMaxLength(36).IsRequired();
            builder.Property(u => u.UserName).HasMaxLength(36).IsRequired();
            builder.Property(u => u.ProfileImageName).HasMaxLength(64);
        }
    }
}
