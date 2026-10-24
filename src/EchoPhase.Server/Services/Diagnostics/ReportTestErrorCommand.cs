// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Cli.Commands;
using Sentry;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EchoPhase.Server.Services.Diagnostics
{
    public class ReportTestErrorSettings : CommandSettings
    {
    }

    /// <summary>
    /// Sends one error event through the configured Sentry SDK (SENTRY_DSN)
    /// and waits until it is delivered: after a deploy, does GlitchTip get the
    /// server's errors? Lives here, not in EchoPhase.Cli, because the SDK does.
    /// </summary>
    public class ReportTestErrorCommand : AsyncCommand<ReportTestErrorSettings>
    {
        private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(10);

        private readonly IHub _hub;

        // Resolving the hub initializes the SDK, as the server's start would.
        public ReportTestErrorCommand(IHub hub)
        {
            _hub = hub;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, ReportTestErrorSettings settings, CancellationToken cancellationToken)
        {
            if (!_hub.IsEnabled)
            {
                AnsiConsole.MarkupLine("[red]Error reporting is off: SENTRY_DSN is not set.[/]");
                return CliExit.Failure;
            }

            var id = _hub.CaptureMessage("EchoPhase test error (report-test-error)", SentryLevel.Error);
            await _hub.FlushAsync(FlushTimeout);
            AnsiConsole.MarkupLineInterpolated($"[green]Sent event {id:N}; look for it in GlitchTip, project server.[/]");
            return CliExit.Success;
        }
    }
}
