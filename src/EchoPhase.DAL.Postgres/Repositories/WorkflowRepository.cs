// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Queries;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class WorkflowRepository : RepositoryBase<Workflow>
    {
        private readonly PostgresContext _context;

        public WorkflowRepository(PostgresContext context)
        {
            _context = context;
        }

        public new WorkflowQuery Query() => new(Build());

        public override IQueryable<Workflow> Build()
            => _context.Workflows;

        /// <summary>Schedules of active workflows due at <paramref name="now"/> (UTC), with their workflow.</summary>
        public Task<List<WorkflowSchedule>> DueSchedulesAsync(DateTime now, int limit, CancellationToken ct = default)
            => _context.WorkflowSchedules
                .Include(s => s.Workflow)
                .Where(s => s.NextRunAt <= now && s.Workflow != null && s.Workflow.Active)
                .OrderBy(s => s.NextRunAt)
                .Take(limit)
                .ToListAsync(ct);

        /// <summary>Replaces a workflow's schedules (they follow its definition).</summary>
        public async Task ReplaceSchedulesAsync(Guid workflowId, IEnumerable<WorkflowSchedule> schedules, CancellationToken ct = default)
        {
            var existing = await _context.WorkflowSchedules.Where(s => s.WorkflowId == workflowId).ToListAsync(ct);
            _context.WorkflowSchedules.RemoveRange(existing);
            _context.WorkflowSchedules.AddRange(schedules);
        }

        public void AddVersion(WorkflowVersion version) => _context.WorkflowVersions.Add(version);

        /// <summary>A workflow's versions, newest first, without their definitions.</summary>
        public Task<List<WorkflowVersion>> VersionsAsync(Guid workflowId, CancellationToken ct = default)
            => _context.WorkflowVersions
                .Where(v => v.WorkflowId == workflowId)
                .OrderByDescending(v => v.Version)
                .Select(v => new WorkflowVersion
                {
                    Id = v.Id,
                    WorkflowId = v.WorkflowId,
                    Version = v.Version,
                    Name = v.Name,
                    ContentHash = v.ContentHash,
                    ParentVersion = v.ParentVersion,
                    RestoredFrom = v.RestoredFrom,
                    SavedBy = v.SavedBy,
                    CreatedAt = v.CreatedAt
                })
                .ToListAsync(ct);

        public Task<WorkflowVersion?> VersionAsync(Guid workflowId, int version, CancellationToken ct = default)
            => _context.WorkflowVersions.FirstOrDefaultAsync(v => v.WorkflowId == workflowId && v.Version == version, ct);

        /// <summary>Deletes all but the newest <paramref name="keep"/> versions.</summary>
        public async Task<int> PruneVersionsAsync(Guid workflowId, int keep, CancellationToken ct = default)
        {
            // A save adds one version, so there are a few at most to delete.
            var old = await _context.WorkflowVersions
                .Where(v => v.WorkflowId == workflowId)
                .OrderByDescending(v => v.Version)
                .Skip(keep)
                .ToListAsync(ct);
            _context.WorkflowVersions.RemoveRange(old);
            await _context.SaveChangesAsync(ct);
            return old.Count;
        }

        public void RemoveSchedule(WorkflowSchedule schedule) => _context.WorkflowSchedules.Remove(schedule);

        /// <summary>One of the user's packages, tracked; null when it is not theirs.</summary>
        public Task<WorkflowPackage?> PackageAsync(Guid ownerId, Guid id, CancellationToken ct = default)
            => _context.WorkflowPackages.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId, ct);

        public void AddPackage(WorkflowPackage package) => _context.WorkflowPackages.Add(package);

        public override void Add(Workflow entity) => _context.Workflows.Add(entity);
        public override void Update(Workflow entity) => _context.Workflows.Update(entity);
        public override void Remove(Workflow entity) => _context.Workflows.Remove(entity);
        public override Task<int> SaveAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
