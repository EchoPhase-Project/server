// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Notes;
using EchoPhase.Notes;
using Microsoft.Extensions.Options;

namespace EchoPhase.Server.Services.Notes
{
    /// <summary>
    /// Sends notes' reminders when they are due: looks for them every
    /// <see cref="NotesOptions.ReminderInterval"/>, a batch at a time.
    /// </summary>
    public class NoteReminderWorker : BackgroundService
    {
        private const int Batch = 200;

        private readonly IServiceScopeFactory _scopes;
        private readonly NotesOptions _options;
        private readonly ILogger<NoteReminderWorker> _logger;

        public NoteReminderWorker(IServiceScopeFactory scopes, IOptions<NotesOptions> options, ILogger<NoteReminderWorker> logger)
        {
            _scopes = scopes;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_options.ReminderInterval);

            do
            {
                try
                {
                    int sent;
                    do
                    {
                        await using var scope = _scopes.CreateAsyncScope();
                        sent = await scope.ServiceProvider.GetRequiredService<NoteService>()
                            .SendDueRemindersAsync(Batch, stoppingToken);
                    }
                    while (sent == Batch);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Not cleared on failure: sent on the next tick.
                    _logger.LogError(ex, "Sending note reminders failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
