// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace EchoPhase.Cli.Commands.Health.Check
{
    public class CheckSettings : CommandSettings
    {
        [CommandOption("--url <URL>")]
        [DefaultValue("http://localhost:8080/health/live")]
        [Description("Health endpoint to call")]
        public string Url { get; set; } = "http://localhost:8080/health/live";

        [CommandOption("--verbose|-v")]
        [DefaultValue(false)]
        [Description("Show command output")]
        public bool Verbose { get; set; } = false;

        public override ValidationResult Validate()
        {
            return ValidationResult.Success();
        }
    }
}
