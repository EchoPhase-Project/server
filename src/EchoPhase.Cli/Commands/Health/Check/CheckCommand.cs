// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Cli.Commands.Health.Check
{
    public class CheckCommand : AsyncCommand<CheckSettings>
    {
        public CheckCommand()
        {
        }

        public override async Task<int> ExecuteAsync(CommandContext context, CheckSettings settings, CancellationToken cancellationToken)
        {
            var url = "http://localhost:8080/health/live";

            try
            {
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(2)
                };

                using var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    if (settings.Verbose)
                        AnsiConsole.MarkupLine("[red]Unhealthy[/]");

                    return 1;
                }

                if (settings.Verbose)
                    AnsiConsole.MarkupLine("[green]Healthy[/]");
                return 0;
            }
            catch
            {
                return 1;
            }
        }
    }
}
