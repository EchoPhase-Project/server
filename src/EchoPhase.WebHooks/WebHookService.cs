// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq.Expressions;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using EchoPhase.DAL.Abstractions.Service;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Identity;
using EchoPhase.Security.BitMasks;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Types.Extensions;

namespace EchoPhase.WebHooks
{
    public class WebHookService : DataServiceBase<WebHook, WebHookRepository>
    {
        private readonly PostgresContext _context;
        private readonly HttpClient _httpClient;
        private readonly IUserService _userService;
        private readonly IRoleService _roleService;
        private readonly IUserPermissionService _permissions;

        public WebHookService(
            PostgresContext context,
            HttpClient httpClient,
            IUserService userService,
            IRoleService roleService,
            IUserPermissionService permissions,
            WebHookRepository repository
        ) : base(repository)
        {
            _context = context;
            _httpClient = httpClient;
            _userService = userService;
            _roleService = roleService;
            _permissions = permissions;
        }

        public async Task<IEnumerable<WebHook>> CreateAsync(IEnumerable<WebHook> webhooks, CancellationToken ct = default)
        {
            foreach (var webhook in webhooks)
            {
                if (!await ValidateIntentsAsync(webhook, ct))
                    continue;

                await _context.WebHooks.AddAsync(webhook, ct);
            }

            await _context.SaveChangesAsync(ct);

            return webhooks;
        }

        public async Task<WebHook> EditAsync(
            WebHook webhook,
            WebHook modifyData,
            Expression<Func<WebHook, object>>[] overrideFields,
            CancellationToken ct = default
        )
        {
            webhook.MergeFrom(modifyData, overrideFields);
            // An edit must not widen the subscription past the owner's permissions.
            await ValidateIntentsAsync(webhook, ct);
            _context.WebHooks.Update(webhook);

            await _context.SaveChangesAsync(ct);

            return webhook;
        }

        public async Task<IEnumerable<WebHook>> EditAsync(
            IEnumerable<WebHook> webhooks,
            WebHook modifyData,
            Expression<Func<WebHook, object>>[] overrideFields,
            CancellationToken ct = default
        )
        {
            var edited = new HashSet<WebHook>();
            foreach (var webhook in webhooks)
                edited.Add(await EditAsync(webhook, modifyData, overrideFields, ct));

            return edited;
        }

        public async Task<IEnumerable<WebHook>> DeleteAsync(IEnumerable<WebHook> webhooks, CancellationToken ct = default)
        {
            foreach (var webhook in webhooks)
                _context.WebHooks.Remove(webhook);

            await _context.SaveChangesAsync(ct);

            return webhooks;
        }

        public async Task SendMessageToAllAsync<T>(
            T message,
            HashSet<string> intents,
            string contentType = MediaTypeNames.Application.Json,
            CancellationToken ct = default)
        {
            var webhooks = await Repository.Query()
                .WithStatus(WebHookStatus.Enabled)
                .WithIntents(intents.ToArray())
                .ToListAsync(ct);

            var content = WrapMessage(message, Encoding.UTF8, contentType);
            await PostMessageToWebHooksAsync(content, webhooks, ct);
        }

        public async Task SendMessageToUsersAsync<T>(
            HashSet<Guid> userIds,
            T message,
            HashSet<string> intents,
            string contentType = MediaTypeNames.Application.Json,
            CancellationToken ct = default)
        {
            var webhooks = await Repository.Query()
                .WithUserIds(userIds.ToArray())
                .WithStatus(WebHookStatus.Enabled)
                .WithIntents(intents.ToArray())
                .ToListAsync(ct);

            var content = WrapMessage(message, Encoding.UTF8, contentType);
            await PostMessageToWebHooksAsync(content, webhooks, ct);
        }

        public async Task SendMessageToRolesAsync<T>(
            IEnumerable<string> roles,
            T message,
            HashSet<string> intents,
            string contentType = MediaTypeNames.Application.Json,
            CancellationToken ct = default)
        {
            var usersWithRole = await _roleService.GetUsersInRolesAsync(roles);

            var webhooks = await Repository.Query()
                .WithUserIds(usersWithRole.Select(u => u.Id).ToArray())
                .WithStatus(WebHookStatus.Enabled)
                .WithIntents(intents.ToArray())
                .ToListAsync(ct);

            var content = WrapMessage(message, Encoding.UTF8, contentType);
            await PostMessageToWebHooksAsync(content, webhooks, ct);
        }

        /// <summary>
        /// Same rule as WebSocket subscriptions: intents must exist and the
        /// owner's permissions (roles plus grants) decide which ones a webhook may receive.
        /// Normalizes the list; false when the owner no longer exists.
        /// </summary>
        private async Task<bool> ValidateIntentsAsync(WebHook webhook, CancellationToken ct)
        {
            var owner = await _context.Users.FindAsync([webhook.UserId], ct);
            if (owner is null)
                return false;

            var requested = webhook.Intents.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

            var unknown = requested.Except(Intents.AsEnumerable(), StringComparer.Ordinal).ToList();
            if (unknown.Count > 0)
                throw new ArgumentException(
                    $"Webhook '{webhook.Name}' requests unknown intents: {string.Join(", ", unknown)}.");

            var ceiling = await _permissions.GetCeilingAsync(owner, ct);
            var disallowed = IntentRequirements.Disallowed(requested, ceiling);
            if (disallowed.Count > 0)
                throw new ArgumentException(
                    $"Webhook '{webhook.Name}' requests intents its owner may not receive: {string.Join(", ", disallowed)}.");

            webhook.Intents = requested;
            return true;
        }

        private async Task PostMessageToWebHooksAsync(StringContent content, IEnumerable<WebHook> webhooks, CancellationToken ct)
        {
            foreach (var webhook in webhooks)
                await _httpClient.PostAsync(webhook.Url, content, ct);
        }

        private StringContent WrapMessage<T>(T message, Encoding encoding, string contentType) =>
            new(SerializeMessage(message), encoding, contentType);

        private string SerializeMessage<T>(T message) =>
            message is string str ? str : JsonSerializer.Serialize(message);
    }
}
