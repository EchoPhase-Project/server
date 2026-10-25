// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class WorkflowPackageConfiguration : IEntityTypeConfiguration<WorkflowPackage>
    {
        public const int MaxNameLength = 100;
        public const int MaxSummaryLength = 300;
        public const int MaxReadmeLength = 100_000;
        public const int MaxTags = 10;
        public const int MaxTagLength = 32;

        public void Configure(EntityTypeBuilder<WorkflowPackage> builder)
        {
            builder.ToTable("WorkflowPackages", PostgresContext.DefaultSchema);
            builder.HasKey(p => p.Id);
            builder.HasIndex(p => p.OwnerId);
            // Explore: public packages.
            builder.HasIndex(p => p.Visibility);
            builder.HasIndex(p => p.Tags).HasMethod("gin");

            builder.Property(p => p.Name).HasMaxLength(MaxNameLength).IsRequired();
            builder.Property(p => p.Summary).HasMaxLength(MaxSummaryLength).IsRequired();
            builder.Property(p => p.Readme).HasMaxLength(MaxReadmeLength).IsRequired();
            builder.Property(p => p.Visibility).HasConversion<string>().HasMaxLength(16);

            builder.HasOne(p => p.Owner)
                .WithMany()
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class WorkflowPackageVersionConfiguration : IEntityTypeConfiguration<WorkflowPackageVersion>
    {
        public const int MaxChangelogLength = 10_000;

        public void Configure(EntityTypeBuilder<WorkflowPackageVersion> builder)
        {
            builder.ToTable("WorkflowPackageVersions", PostgresContext.DefaultSchema);
            builder.HasKey(v => v.Id);
            builder.HasIndex(v => new { v.PackageId, v.Version }).IsUnique();

            builder.Property(v => v.Changelog).HasMaxLength(MaxChangelogLength).IsRequired();
            builder.Property(v => v.ContentHash).HasMaxLength(64).IsRequired();
            builder.Property(v => v.Content).HasColumnType("jsonb").IsRequired();

            builder.HasOne(v => v.Package)
                .WithMany(p => p.Versions)
                .HasForeignKey(v => v.PackageId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class WorkflowPackageShareConfiguration : IEntityTypeConfiguration<WorkflowPackageShare>
    {
        public void Configure(EntityTypeBuilder<WorkflowPackageShare> builder)
        {
            builder.ToTable("WorkflowPackageShares", PostgresContext.DefaultSchema);
            builder.HasKey(s => new { s.PackageId, s.UserId });
            builder.HasIndex(s => s.UserId);

            builder.HasOne(s => s.Package)
                .WithMany(p => p.Shares)
                .HasForeignKey(s => s.PackageId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class WorkflowPackageInstallConfiguration : IEntityTypeConfiguration<WorkflowPackageInstall>
    {
        public void Configure(EntityTypeBuilder<WorkflowPackageInstall> builder)
        {
            builder.ToTable("WorkflowPackageInstalls", PostgresContext.DefaultSchema);
            builder.HasKey(i => new { i.PackageId, i.UserId });
            builder.HasIndex(i => i.UserId);

            builder.HasOne(i => i.Package)
                .WithMany(p => p.Installs)
                .HasForeignKey(i => i.PackageId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
