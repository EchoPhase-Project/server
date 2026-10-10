// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Controllers.Api.v1.Workflows
{
    /// <param name="Name">Display name.</param>
    /// <param name="Program">Blocks, connections and settings.</param>
    /// <param name="Active">Triggers (webhook, schedule) start runs only while active.</param>
    /// <param name="Version">On update: the version you edited; a newer one on the server is a 409.</param>
    public sealed record SaveWorkflowRequest(
        [Required][MaxLength(100)] string Name,
        [Required] BlockProgram Program,
        bool Active = false,
        int? Version = null);

    /// <param name="Input">The first blocks' input (a ManualTrigger's output).</param>
    public sealed record RunWorkflowRequest(JsonElement? Input = null);

    /// <param name="CurrentVersion">The version you are looking at; a newer one on the server is a 409.</param>
    public sealed record RestoreWorkflowRequest(int? CurrentVersion = null);
}
