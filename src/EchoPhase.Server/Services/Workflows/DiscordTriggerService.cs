// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Text.Json;
using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Discord.WebSocket;
using EchoPhase.Connections;
using EchoPhase.Connections.Services;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Handlers;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Scripting;
using EchoPhase.Server.Services.Blocks;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.Server.Services.Workflows
{
    /// <summary>
    /// Runs workflows on Discord gateway events. For each owner with active
    /// workflows that have a DiscordEventTrigger, it keeps one gateway
    /// connection per bot connection open (with the intents those triggers
    /// need) and starts a run for every matching event. Only events from its
    /// own connections count, so a connection opened by hand does not double
    /// them. Every <see cref="Interval"/> it reloads triggers and reopens
    /// dropped connections. Single instance, like the scheduler.
    /// </summary>
    public sealed class DiscordTriggerService : BackgroundService, IDiscordWebSocketSubscriber, IDiscordGatewayObserver
    {
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        /// <summary>How long a connection that failed for good (bad token, intents) is left closed.</summary>
        public static readonly TimeSpan FailureHold = TimeSpan.FromMinutes(30);

        private sealed record Trigger(Guid WorkflowId, int BlockId, BlockProgram Program, DiscordEventTriggerParams Params);

        private sealed record OpenConnection(Guid Id, int Intents);

        private readonly DiscordWebSocketManager _gateway;
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<DiscordTriggerService> _logger;
        private readonly SemaphoreSlim _reload = new(1, 1);

        // owner -> bot connection name -> triggers
        private readonly ConcurrentDictionary<Guid, IReadOnlyDictionary<string, List<Trigger>>> _triggers = new();
        // connections this service opened (and so listens to)
        private readonly ConcurrentDictionary<(Guid UserId, string Connection), OpenConnection> _open = new();
        private readonly ConcurrentDictionary<Guid, DiscordWebSocketSubscription> _subscriptions = new();
        // connections that failed for good, and until when not to reopen them
        private readonly ConcurrentDictionary<(Guid UserId, string Connection), DateTimeOffset> _failed = new();

        public DiscordTriggerService(
            DiscordWebSocketManager gateway, IServiceScopeFactory scopes, ILogger<DiscordTriggerService> logger)
        {
            _gateway = gateway;
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _gateway.AddObserver(this);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    await ReloadAllAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Discord triggers could not be reloaded.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        /// <summary>
        /// Picks up a user's changed workflows now (called after saves). A save
        /// is also the user's try again: failed connections are reopened.
        /// </summary>
        public async Task ReloadUserAsync(Guid userId, CancellationToken ct = default)
        {
            foreach (var key in _failed.Keys.Where(k => k.UserId == userId).ToList())
                _failed.TryRemove(key, out _);

            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var workflows = scope.ServiceProvider.GetRequiredService<WorkflowRepository>();
                var active = await workflows.Query().WithUserIds(userId).Where(w => w.Active).ToListAsync(ct);
                await ApplyAsync(userId, Collect(active), scope.ServiceProvider, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Discord triggers of user {UserId} could not be reloaded.", userId);
            }
        }

        private async Task ReloadAllAsync(CancellationToken ct)
        {
            await using var scope = _scopes.CreateAsyncScope();
            var workflows = scope.ServiceProvider.GetRequiredService<WorkflowRepository>();
            var active = await workflows.Query().Where(w => w.Active).ToListAsync(ct);

            var byUser = active.GroupBy(w => w.UserId).ToDictionary(g => g.Key, g => Collect(g));
            foreach (var userId in byUser.Keys.Union(_triggers.Keys).ToList())
                await ApplyAsync(userId, byUser.GetValueOrDefault(userId) ?? new(), scope.ServiceProvider, ct);
        }

        private static Dictionary<string, List<Trigger>> Collect(IEnumerable<DAL.Postgres.Models.Workflow> workflows)
        {
            var result = new Dictionary<string, List<Trigger>>(StringComparer.Ordinal);
            foreach (var workflow in workflows)
            {
                var program = WorkflowService.Parse(workflow);
                foreach (var block in program.Blocks.Where(b => b.Type == BlockTypes.DiscordEventTrigger && !b.Disabled))
                {
                    var param = (DiscordEventTriggerParams)BlockParamsBinder.Bind(typeof(DiscordEventTriggerParams), block.Params);
                    if (!result.TryGetValue(param.Connection, out var list))
                        result[param.Connection] = list = [];
                    list.Add(new Trigger(workflow.Id, block.Id, program, param));
                }
            }
            return result;
        }

        // Makes the gateway match the triggers: the right connections open
        // with the right intents, the rest of ours closed.
        private async Task ApplyAsync(
            Guid userId, Dictionary<string, List<Trigger>> triggers, IServiceProvider services, CancellationToken ct)
        {
            await _reload.WaitAsync(ct);
            try
            {
                _triggers[userId] = triggers;
                if (triggers.Count > 0)
                    _subscriptions.GetOrAdd(userId, id => _gateway.Subscribe(id, this));
                else if (_subscriptions.TryRemove(userId, out var subscription))
                    subscription.Dispose();

                var alive = _gateway.GetConnections(userId);
                foreach (var ((owner, name), open) in _open.Where(o => o.Key.UserId == userId).ToList())
                {
                    bool stillOpen = alive.TryGetValue(name, out var ids) && ids.Contains(open.Id);
                    bool wanted = triggers.TryGetValue(name, out var list)
                                  && DiscordGatewayIntents.For(list.SelectMany(t => t.Params.Events)) == open.Intents;
                    if (stillOpen && wanted)
                        continue;
                    if (stillOpen)
                        await _gateway.DisconnectAsync(owner, open.Id);
                    _open.TryRemove((owner, name), out _);
                }

                var connections = services.GetRequiredService<IConnectionService>();
                foreach (var (name, list) in triggers)
                {
                    if (_open.ContainsKey((userId, name)))
                        continue;
                    if (_failed.TryGetValue((userId, name), out var until) && until > DateTimeOffset.UtcNow)
                        continue;

                    var credential = await connections.ResolveAsync(new ConnectionRef(userId, name), ConnectionProviders.Discord, ct);
                    if (!credential.Successful)
                    {
                        _logger.LogWarning("Discord trigger connection {Connection} of user {UserId} unavailable: {Error}",
                            name, userId, credential.Error.Value);
                        continue;
                    }

                    var intents = DiscordGatewayIntents.For(list.SelectMany(t => t.Params.Events));
                    var id = await _gateway.ConnectAsync(userId, name, credential.Value!.AccessToken, intents, ct);
                    _open[(userId, name)] = new OpenConnection(id, intents);
                    _logger.LogInformation("Opened Discord gateway {ConnectionId} for triggers of user {UserId} ({Connection}).",
                        id, userId, name);
                }
            }
            finally
            {
                _reload.Release();
            }
        }

        public async Task OnPayloadAsync(
            Guid userId, string tokenName, Guid connectionId, JsonDocument payload, CancellationToken ct = default)
        {
            var root = payload.RootElement;
            if (root.GetProperty("op").GetInt32() != 0 || !root.TryGetProperty("t", out var t)
                || !_open.TryGetValue((userId, tokenName), out var open) || open.Id != connectionId
                || !_triggers.TryGetValue(userId, out var byConnection) || !byConnection.TryGetValue(tokenName, out var triggers))
                return;

            var eventName = t.GetString() ?? string.Empty;
            var data = root.TryGetProperty("d", out var d) ? d : default;

            foreach (var trigger in triggers.Where(tr => tr.Params.Events.Contains(eventName) && Matches(tr.Params, data)))
            {
                var input = new Dictionary<string, object?>
                {
                    ["event"] = eventName,
                    ["connection"] = tokenName,
                    ["data"] = data.ValueKind == JsonValueKind.Undefined ? null : Eval.Plain(data)
                };

                await using var scope = _scopes.CreateAsyncScope();
                var runs = scope.ServiceProvider.GetRequiredService<BlockRunService>();
                var result = await runs.StartAsync(userId, null, new BlockRunStart(
                    trigger.Program, [trigger.BlockId], input, trigger.WorkflowId, BlockRunTriggers.Discord), ct);

                if (!result.Successful)
                    _logger.LogWarning("Discord event {Event} did not start workflow {WorkflowId}: {Error}",
                        eventName, trigger.WorkflowId, result.Error.Value);
            }
        }

        /// <summary>A connection of ours stopped for good: forget it and hold off reopening.</summary>
        public Task OnFailedAsync(DiscordGatewayFailure failure, CancellationToken ct = default)
        {
            var key = (failure.UserId, failure.TokenName);
            if (_open.TryGetValue(key, out var open) && open.Id == failure.ConnectionId)
            {
                _open.TryRemove(key, out _);
                _failed[key] = DateTimeOffset.UtcNow + FailureHold;
            }
            return Task.CompletedTask;
        }

        private static bool Matches(DiscordEventTriggerParams filter, JsonElement data)
        {
            if (data.ValueKind != JsonValueKind.Object)
                return filter.GuildId is null && filter.ChannelId is null;

            static string? Text(JsonElement e, string name)
                => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            if (filter.GuildId is { } guild && Text(data, "guild_id") != guild)
                return false;
            if (filter.ChannelId is { } channel && Text(data, "channel_id") != channel)
                return false;

            // Messages carry author, members user: either says whether a bot did it.
            if (filter.IgnoreBots)
                foreach (var who in new[] { "author", "user" })
                    if (data.TryGetProperty(who, out var person) && person.ValueKind == JsonValueKind.Object
                        && person.TryGetProperty("bot", out var bot) && bot.ValueKind == JsonValueKind.True)
                        return false;

            return true;
        }

        public override void Dispose()
        {
            foreach (var subscription in _subscriptions.Values)
                subscription.Dispose();
            _subscriptions.Clear();
            _reload.Dispose();
            base.Dispose();
        }
    }
}
