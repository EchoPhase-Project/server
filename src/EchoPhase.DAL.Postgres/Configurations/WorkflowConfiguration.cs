// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EchoPhase.DAL.Postgres.Configurations
{
    public class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
    {
        public void Configure(EntityTypeBuilder<Workflow> builder)
        {
            builder.ToTable("Workflows", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => e.UserId);

            builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
            builder.Property(e => e.Definition).HasColumnType("jsonb").IsRequired();

            builder.HasOne(e => e.User)
                .WithMany(u => u.Workflows)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class WorkflowScheduleConfiguration : IEntityTypeConfiguration<WorkflowSchedule>
    {
        public void Configure(EntityTypeBuilder<WorkflowSchedule> builder)
        {
            builder.ToTable("WorkflowSchedules", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new { e.WorkflowId, e.BlockId }).IsUnique();
            // The scheduler's query: everything due.
            builder.HasIndex(e => e.NextRunAt);

            builder.Property(e => e.Cron).HasMaxLength(100).IsRequired();
            builder.Property(e => e.TimeZone).HasMaxLength(64).IsRequired();

            builder.HasOne(e => e.Workflow)
                .WithMany(w => w.Schedules)
                .HasForeignKey(e => e.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class WorkflowVersionConfiguration : IEntityTypeConfiguration<WorkflowVersion>
    {
        public void Configure(EntityTypeBuilder<WorkflowVersion> builder)
        {
            builder.ToTable("WorkflowVersions", PostgresContext.DefaultSchema);
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new { e.WorkflowId, e.Version }).IsUnique();

            builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
            builder.Property(e => e.Definition).HasColumnType("jsonb").IsRequired();

            builder.HasOne(e => e.Workflow)
                .WithMany(w => w.Versions)
                .HasForeignKey(e => e.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
