// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Builders;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.DAL.Scylla.Models;

namespace EchoPhase.DAL.Scylla
{
    public class ScyllaContext : DbContext
    {
        public DbSet<BlockRun> BlockRuns => Set<BlockRun>();
        public DbSet<BlockRunResult> BlockRunResults => Set<BlockRunResult>();
        public DbSet<ActiveBlockRun> ActiveBlockRuns => Set<ActiveBlockRun>();
        public DbSet<Notification> Notifications => Set<Notification>();

        public ScyllaContext(ScyllaConnection connection) : base(connection)
        {
        }

        protected override void OnModelBuilding(ModelBuilder builder)
        {
            base.OnModelBuilding(builder);
            Configure(builder);
        }

        /// <summary>The application's model; public so it can be checked without a cluster.</summary>
        public static void Configure(ModelBuilder builder)
        {
            builder.Entity<BlockRun>()
                .ToTable("block_runs")
                .HasPartitionKey<Guid>(x => x.UserId)
                .HasClusteringKey<Guid>(x => x.RunId);
            Columns(builder.Entity<BlockRun>(), new()
            {
                [nameof(BlockRun.UserId)] = "user_id",
                [nameof(BlockRun.RunId)] = "run_id",
                [nameof(BlockRun.Status)] = "status",
                [nameof(BlockRun.ActorId)] = "actor_id",
                [nameof(BlockRun.WorkflowId)] = "workflow_id",
                [nameof(BlockRun.Trigger)] = "trigger",
                [nameof(BlockRun.ProgramHash)] = "program_hash",
                [nameof(BlockRun.BlockCount)] = "block_count",
                [nameof(BlockRun.CreatedAt)] = "created_at",
                [nameof(BlockRun.StartedAt)] = "started_at",
                [nameof(BlockRun.FinishedAt)] = "finished_at",
                [nameof(BlockRun.DurationMs)] = "duration_ms",
                [nameof(BlockRun.Steps)] = "steps",
                [nameof(BlockRun.ErrorCount)] = "error_count",
            });

            builder.Entity<BlockRunResult>()
                .ToTable("block_run_results")
                .HasPartitionKey<Guid>(x => x.UserId)
                .HasClusteringKey<Guid>(x => x.RunId);
            Columns(builder.Entity<BlockRunResult>(), new()
            {
                [nameof(BlockRunResult.UserId)] = "user_id",
                [nameof(BlockRunResult.RunId)] = "run_id",
                [nameof(BlockRunResult.Payload)] = "payload",
            });

            builder.Entity<ActiveBlockRun>()
                .ToTable("block_runs_active")
                .HasPartitionKey<Guid>(x => x.RunId);
            Columns(builder.Entity<ActiveBlockRun>(), new()
            {
                [nameof(ActiveBlockRun.RunId)] = "run_id",
                [nameof(ActiveBlockRun.UserId)] = "user_id",
            });

            builder.Entity<Notification>()
                .ToTable("notifications")
                .HasPartitionKey<Guid>(x => x.UserId)
                .HasClusteringKey<Guid>(x => x.Id);
            Columns(builder.Entity<Notification>(), new()
            {
                [nameof(Notification.UserId)] = "user_id",
                [nameof(Notification.Id)] = "id",
                [nameof(Notification.Kind)] = "kind",
                [nameof(Notification.Title)] = "title",
                [nameof(Notification.Body)] = "body",
                [nameof(Notification.Link)] = "link",
                [nameof(Notification.Params)] = "params",
                [nameof(Notification.Read)] = "read",
                [nameof(Notification.CreatedAt)] = "created_at",
            });
            builder.Entity<Notification>().Property(x => x.Params)
                .HasConversion(ValueConverters.Json<Dictionary<string, string>>());
        }

        private static void Columns<TEntity>(EntityBuilder<TEntity> entity, Dictionary<string, string> columns)
            where TEntity : class
        {
            foreach (var (property, column) in columns)
                entity.SetColumnName(property, column);
        }
    }
}
