// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Models
{
    public class BlockError
    {
        public int Id
        {
            get; set;
        }
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        /// <summary>As expressions see it: { block, type, message }.</summary>
        public Dictionary<string, object?> ToValue() => new()
        {
            ["block"] = Id,
            ["type"] = Type,
            ["message"] = Message
        };
    }
}
