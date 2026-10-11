// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using EchoPhase.Configuration.Argon2;
using EchoPhase.Configuration.Authentication;
using EchoPhase.Configuration.Cryptography;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Scylla.Database;
using EchoPhase.Security.Cryptography.Vaults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoPhase.Server.Services.Health
{
    /// <summary>
    /// Pays the cold start before traffic arrives: loads every key from the
    /// vault (opening Redis), builds the EF model and opens a Postgres
    /// connection, connects to Scylla and runs one password hash (JIT). Steps
    /// that fail are retried until all succeed; only then is the pod ready.
    /// </summary>
    public sealed class WarmupService : BackgroundService
    {
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

        private readonly IServiceScopeFactory _scopes;
        private readonly WarmupState _state;
        private readonly ILogger<WarmupService> _logger;

        public WarmupService(IServiceScopeFactory scopes, WarmupState state, ILogger<WarmupService> logger)
        {
            _scopes = scopes;
            _state = state;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var pending = new List<(string Name, Func<IServiceProvider, CancellationToken, Task> Run)>
            {
                ("keys", LoadKeysAsync),
                ("postgres", WarmPostgresAsync),
                ("scylla", (sp, _) => { sp.GetRequiredService<ScyllaConnection>(); return Task.CompletedTask; }),
                ("password hash", (sp, _) =>
                {
                    sp.GetRequiredService<IPasswordHasher<User>>().HashPassword(new User("warmup"), "warmup");
                    return Task.CompletedTask;
                }),
            };

            var total = Stopwatch.StartNew();
            while (pending.Count > 0 && !stoppingToken.IsCancellationRequested)
            {
                await using var scope = _scopes.CreateAsyncScope();

                foreach (var step in pending.ToList())
                {
                    var watch = Stopwatch.StartNew();
                    try
                    {
                        await step.Run(scope.ServiceProvider, stoppingToken);
                        pending.Remove(step);
                        _logger.LogInformation("Warm-up: {Step} in {ElapsedMs} ms.", step.Name, watch.ElapsedMilliseconds);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "Warm-up: {Step} failed; retrying.", step.Name);
                    }
                }

                if (pending.Count > 0)
                    await Task.Delay(RetryDelay, stoppingToken);
            }

            if (pending.Count == 0)
            {
                _state.MarkReady();
                _logger.LogInformation("Warm-up finished in {ElapsedMs} ms; ready for traffic.", total.ElapsedMilliseconds);
            }
        }

        private static Task LoadKeysAsync(IServiceProvider sp, CancellationToken ct)
        {
            var keys = sp.GetRequiredService<KeyRing>();
            var auth = sp.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

            keys.Get(auth.Bearer.Key);
            keys.Get(auth.Refresh.Key);
            keys.Get(auth.ApiClients.Key);
            keys.Get(sp.GetRequiredService<IOptions<CryptographyOptions>>().Value.Aes.Key);
            keys.Get(sp.GetRequiredService<IOptions<Argon2Options>>().Value.Key);
            return Task.CompletedTask;
        }

        // The first query builds the EF model, which is the slow part.
        private static Task WarmPostgresAsync(IServiceProvider sp, CancellationToken ct)
            => sp.GetRequiredService<PostgresContext>().Users.AsNoTracking().AnyAsync(ct);
    }
}
