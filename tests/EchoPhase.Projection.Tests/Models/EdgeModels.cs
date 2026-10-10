// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Projection.Attributes;

namespace EchoPhase.Projection.Tests.Models
{
    public class ValueTypesModel
    {
        [Expose] public DateOnly Day { get; set; } = new(2026, 1, 2);
        [Expose] public TimeOnly Time { get; set; } = new(3, 4);
        [Expose] public DateTime? MaybeAt { get; set; } = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        [Expose] public Uri Link { get; set; } = new("https://example.org/");
        [Expose] public Dictionary<string, int> Counts { get; set; } = new() { ["a"] = 1, ["b"] = 2 };
    }

    public class Category
    {
        [Expose] public string Name { get; set; } = "";
        [Expose] public List<Category> Children { get; set; } = [];
        public string Secret { get; set; } = "hidden";
    }
}
