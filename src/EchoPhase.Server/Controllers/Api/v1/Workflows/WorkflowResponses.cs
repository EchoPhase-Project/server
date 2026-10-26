// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Server.Services.Workflows;

namespace EchoPhase.Server.Controllers.Api.v1.Workflows
{
    /// <param name="PackageId">The package it was installed from (it may be gone since).</param>
    /// <param name="Version">That package's version.</param>
    /// <param name="Key">Which of the package's workflows it is a copy of.</param>
    public sealed record WorkflowSourceResponse(Guid PackageId, int? Version, Guid? Key);

    /// <param name="Id">The workflow's id.</param>
    /// <param name="Name">Its name.</param>
    /// <param name="Active">Its triggers start runs.</param>
    /// <param name="Version">Bumped on every change of its program.</param>
    /// <param name="ContentHash">SHA-256 of its program; null until a workflow saved before hashes is saved again.</param>
    /// <param name="Source">Where it was installed from; null: made here.</param>
    /// <param name="CreatedAt">When it was created.</param>
    /// <param name="UpdatedAt">When it last changed.</param>
    public sealed record WorkflowSummaryResponse(Guid Id, string Name, bool Active, int Version, string? ContentHash,
        WorkflowSourceResponse? Source, DateTime CreatedAt, DateTime UpdatedAt)
    {
        public static WorkflowSummaryResponse From(Workflow w) => new(w.Id, w.Name, w.Active, w.Version, w.ContentHash,
            w.SourcePackageId is { } package ? new WorkflowSourceResponse(package, w.SourcePackageVersion, w.SourceKey) : null,
            w.CreatedAt, w.UpdatedAt);
    }

    /// <param name="BlockId">The WebhookTrigger block.</param>
    /// <param name="Path">Where it listens: POST here with the X-Webhook-Secret header.</param>
    public sealed record WorkflowWebhookResponse(int BlockId, string Path);

    /// <param name="BlockId">The ScheduleTrigger block.</param>
    /// <param name="Cron">Its schedule (five fields).</param>
    /// <param name="TimeZone">The zone the schedule is read in.</param>
    /// <param name="NextRunAt">When it fires next (UTC), whether the workflow is active or not.</param>
    public sealed record WorkflowScheduleResponse(int BlockId, string Cron, string TimeZone, DateTime NextRunAt);

    /// <param name="Id">The workflow's id.</param>
    /// <param name="Name">Its name.</param>
    /// <param name="Active">Its triggers start runs.</param>
    /// <param name="Version">Bumped on every change of its program.</param>
    /// <param name="ContentHash">SHA-256 of its program.</param>
    /// <param name="Source">Where it was installed from; null: made here.</param>
    /// <param name="CreatedAt">When it was created.</param>
    /// <param name="UpdatedAt">When it last changed.</param>
    /// <param name="Program">Its blocks, the edges between them and how it runs.</param>
    /// <param name="Webhooks">Where its WebhookTriggers listen.</param>
    /// <param name="Schedules">Its ScheduleTriggers and when each fires next.</param>
    public sealed record WorkflowResponse(Guid Id, string Name, bool Active, int Version, string ContentHash,
        WorkflowSourceResponse? Source, DateTime CreatedAt, DateTime UpdatedAt, BlockProgram Program,
        IReadOnlyList<WorkflowWebhookResponse> Webhooks, IReadOnlyList<WorkflowScheduleResponse> Schedules)
    {
        public static WorkflowResponse From(WorkflowDetails details)
        {
            var w = details.Workflow;
            return new(w.Id, w.Name, w.Active, w.Version, w.ContentHash ?? BlockProgramHash.Of(details.Program),
                w.SourcePackageId is { } package ? new WorkflowSourceResponse(package, w.SourcePackageVersion, w.SourceKey) : null,
                w.CreatedAt, w.UpdatedAt, details.Program,
                details.Program.Blocks
                    .Where(b => b.Type == BlockTypes.WebhookTrigger)
                    .Select(b => new WorkflowWebhookResponse(b.Id, $"/api/v1/hooks/{w.Id}/{b.Id}"))
                    .ToList(),
                w.Schedules.Select(s => new WorkflowScheduleResponse(s.BlockId, s.Cron, s.TimeZone, s.NextRunAt)).ToList());
        }
    }

    /// <param name="Workflow">The new workflow, inactive.</param>
    /// <param name="Intact">The program matches the hash the file states (false: edited after export).</param>
    public sealed record WorkflowImportResponse(WorkflowResponse Workflow, bool Intact);

    /// <param name="Version">Its number.</param>
    /// <param name="Name">The workflow's name then.</param>
    /// <param name="ContentHash">SHA-256 of its program; null for versions saved before hashes.</param>
    /// <param name="ParentVersion">The version it was made from; null for the first.</param>
    /// <param name="RestoredFrom">The earlier version it brought back, if it did.</param>
    /// <param name="SavedBy">Who saved it.</param>
    /// <param name="CreatedAt">When.</param>
    public sealed record WorkflowVersionResponse(int Version, string Name, string? ContentHash, int? ParentVersion,
        int? RestoredFrom, Guid? SavedBy, DateTime CreatedAt)
    {
        public static WorkflowVersionResponse From(WorkflowVersion v) =>
            new(v.Version, v.Name, v.ContentHash, v.ParentVersion, v.RestoredFrom, v.SavedBy, v.CreatedAt);
    }

    /// <param name="Version">Its number.</param>
    /// <param name="Name">The workflow's name then.</param>
    /// <param name="ContentHash">SHA-256 of its program.</param>
    /// <param name="ParentVersion">The version it was made from; null for the first.</param>
    /// <param name="RestoredFrom">The earlier version it brought back, if it did.</param>
    /// <param name="SavedBy">Who saved it.</param>
    /// <param name="CreatedAt">When.</param>
    /// <param name="Program">The program as it was.</param>
    public sealed record WorkflowVersionDetailsResponse(int Version, string Name, string ContentHash, int? ParentVersion,
        int? RestoredFrom, Guid? SavedBy, DateTime CreatedAt, BlockProgram Program)
    {
        public static WorkflowVersionDetailsResponse From(WorkflowVersionDetails d) => new(d.Version.Version, d.Version.Name,
            d.Version.ContentHash ?? BlockProgramHash.Of(d.Program), d.Version.ParentVersion, d.Version.RestoredFrom,
            d.Version.SavedBy, d.Version.CreatedAt, d.Program);
    }
}
