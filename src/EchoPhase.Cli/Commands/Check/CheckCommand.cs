// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Scylla;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EchoPhase.Cli.Commands.Check
{
    public class CheckSettings : CommandSettings
    {
    }

    /// <summary>
    /// Whether this configuration can run: the settings pass the validation
    /// the server does on start (commands run before it), each store answers,
    /// and no migration is pending.
    /// Exit 1 if anything fails, for an init container or a deploy step.
    /// </summary>
    public class CheckCommand : AsyncCommand<CheckSettings>
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly PostgresContext _postgres;
        private readonly ScyllaContext _scylla;
        private readonly IConnectionMultiplexer _redis;
        private readonly IServiceProvider _services;

        public CheckCommand(PostgresContext postgres, ScyllaContext scylla, IConnectionMultiplexer redis, IServiceProvider services)
        {
            _postgres = postgres;
            _scylla = scylla;
            _redis = redis;
            _services = services;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, CheckSettings settings, CancellationToken cancellationToken)
        {
            var table = new Table().AddColumns("Check", "Result", "Time");
            var failed = false;

            async Task Run(string name, Func<CancellationToken, Task<string>> check)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(Timeout);
                var watch = Stopwatch.StartNew();
                try
                {
                    var result = await check(cts.Token);
                    table.AddRow(name, $"[green]{Markup.Escape(result)}[/]", $"{watch.ElapsedMilliseconds} ms");
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    failed = true;
                    var reason = ex is OperationCanceledException ? $"no answer in {Timeout.TotalSeconds:0} s" : ex.Message;
                    table.AddRow(name, $"[red]{Markup.Escape(reason)}[/]", $"{watch.ElapsedMilliseconds} ms");
                }
            }

            await Run("Configuration", _ =>
            {
                // What ValidateOnStart registered: every IValidatable section.
                _services.GetService<IStartupValidator>()?.Validate();
                return Task.FromResult("valid");
            });

            await Run("Postgres", async ct =>
            {
                if (!await _postgres.Database.CanConnectAsync(ct))
                    throw new InvalidOperationException("cannot connect");
                var pending = (await _postgres.Database.GetPendingMigrationsAsync(ct)).ToList();
                return pending.Count == 0 ? "up to date" : throw new InvalidOperationException($"{pending.Count} migrations pending: run `migrate`");
            });

            await Run("Scylla", async ct =>
            {
                var pending = (await _scylla.Database.GetPendingMigrationsAsync(ct)).ToList();
                return pending.Count == 0 ? "up to date" : throw new InvalidOperationException($"{pending.Count} migrations pending: run `migrate`");
            });

            await Run("Redis", async ct =>
            {
                var latency = await _redis.GetDatabase().PingAsync().WaitAsync(ct);
                return $"ping {latency.TotalMilliseconds:0} ms";
            });

            AnsiConsole.Write(table);
            return failed ? CliExit.Failure : CliExit.Success;
        }
    }
}
