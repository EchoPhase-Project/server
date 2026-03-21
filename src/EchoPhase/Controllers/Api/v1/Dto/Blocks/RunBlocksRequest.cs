// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Controllers.Api.v1.Dto.Blocks
{
    public class RunBlocksRequest
    {
        [Required]
        public HashSet<int> StartIds { get; set; } = [];

        [Required]
        [MinLength(1)]
        public List<Block> Blocks { get; set; } = [];
    }
}
