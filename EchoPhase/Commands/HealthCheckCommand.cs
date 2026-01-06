using EchoPhase.Commands.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EchoPhase.Commands
{
    public class HealthCheckCommand : AsyncCommand<HealthCheckCommandSettings>
    {
        public HealthCheckCommand()
        {
        }

        public override async Task<int> ExecuteAsync(CommandContext context, HealthCheckCommandSettings settings)
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
