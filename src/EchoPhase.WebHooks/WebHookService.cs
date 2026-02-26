using System.Linq.Expressions;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Identity;
using EchoPhase.Types.Service;
using EchoPhase.Types.Extensions;

namespace EchoPhase.WebHooks
{
    public class WebHookService : DataServiceBase<WebHookRepository, WebHookOptions>
    {
        private readonly PostgresContext _context;
        private readonly HttpClient _httpClient;
        private readonly IUserService _userService;
        private readonly IRoleService _roleService;

        public WebHookService(
            PostgresContext context,
            HttpClient httpClient,
            IUserService userService,
            IRoleService roleService,
            WebHookRepository repository
        ) : base(repository)
        {
            _context = context;
            _httpClient = httpClient;
            _userService = userService;
            _roleService = roleService;
        }

        public IEnumerable<WebHook> Get(
            WebHookSearchOptions opts,
            Func<IQueryable<WebHook>, WebHookSearchOptions, IQueryable<WebHook>>? extraFilters = null
        )
        {
            return _repository.Get(opts, extraFilters);
        }

        public IEnumerable<WebHook> Get(
            Action<WebHookSearchOptions> configure,
            Func<IQueryable<WebHook>, WebHookSearchOptions, IQueryable<WebHook>>? extraFilters = null
        )
        {
            return _repository.Get(configure, extraFilters);
        }

        public async Task<IEnumerable<WebHook>> CreateAsync(params IEnumerable<WebHook> webhooks)
        {
            foreach (var webhook in webhooks)
            {
                if (_userService.UserExists(webhook.UserId))
                    await _context.WebHooks.AddAsync(webhook);
            }

            await _context.SaveChangesAsync();

            return webhooks;
        }

        public async Task<WebHook> EditAsync(
            WebHook webhook,
            WebHook modifyData,
            params Expression<Func<WebHook, object>>[] overrideFields
        )
        {
            webhook.MergeFrom(modifyData, overrideFields);
            _context.WebHooks.Update(webhook);

            await _context.SaveChangesAsync();

            return webhook;
        }

        public async Task<IEnumerable<WebHook>> EditAsync(
            IEnumerable<WebHook> webhooks,
            WebHook modifyData,
            params Expression<Func<WebHook, object>>[] overrideFields
        )
        {
            var edited = new HashSet<WebHook>();
            foreach (var webhook in webhooks)
                edited.Add(await EditAsync(webhook, modifyData, overrideFields));

            return edited;
        }

        public async Task<IEnumerable<WebHook>> DeleteAsync(params IEnumerable<WebHook> webhooks)
        {
            foreach (var webhook in webhooks)
                _context.WebHooks.Remove(webhook);

            await _context.SaveChangesAsync();

            return webhooks;
        }

        public async Task SendMessageToAllAsync<T>(T message, ISet<string> intents, string ContentType = MediaTypeNames.Application.Json)
        {
            var webhooks = _repository.Get(opts =>
            {
                opts.Intents = intents;
                opts.Status = WebHookStatus.Enabled;
            });

            var content = WrapMessage(message, Encoding.UTF8, ContentType);
            await PostMessageToWebHooksAsync(content, webhooks);
        }

        public async Task SendMessageToUsersAsync<T>(ISet<Guid> userIds, T message, ISet<string> intents, string ContentType = MediaTypeNames.Application.Json)
        {
            var webhooks = _repository.Get(opts =>
            {
                opts.UserIds = userIds;
                opts.Intents = intents;
                opts.Status = WebHookStatus.Enabled;
            });

            var content = WrapMessage(message, Encoding.UTF8, ContentType);
            await PostMessageToWebHooksAsync(content, webhooks);
        }

        public async Task SendMessageToRolesAsync<T>(IEnumerable<string> roles, T message, ISet<string> intents, string ContentType = MediaTypeNames.Application.Json)
        {
            var usersWithRole = await _roleService.GetUsersInRolesAsync(roles);

            var webhooks = _repository.Get(opts =>
            {
                var userIds = usersWithRole.Select(u => u.Id).ToHashSet();

                opts.UserIds = userIds;
                opts.Intents = intents;
                opts.Status = WebHookStatus.Enabled;
            });

            var content = WrapMessage(message, Encoding.UTF8, ContentType);
            await PostMessageToWebHooksAsync(content, webhooks);
        }

        private async Task PostMessageToWebHooksAsync(StringContent content, IEnumerable<WebHook> webhooks)
        {
            foreach (var webhook in webhooks)
                await _httpClient.PostAsync(webhook.Url, content);
        }

        private StringContent WrapMessage<T>(T message, Encoding encoding, string contentType) =>
            new StringContent(SerializeMessage(message), encoding, contentType);

        private string SerializeMessage<T>(T message) =>
            message is string str ? str : JsonSerializer.Serialize(message);
    }
}
