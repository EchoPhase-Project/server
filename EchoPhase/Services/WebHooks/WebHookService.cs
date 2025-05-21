using System.Text;
using System.Text.Json;

using EchoPhase.DAL.Postgres;
using EchoPhase.Enums;
using EchoPhase.Processors.Enums;
using EchoPhase.Models;
using EchoPhase.Exceptions;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;
using EchoPhase.Repositories;
using EchoPhase.Services.Security;

namespace EchoPhase.Services.WebHooks
{
	public class WebHookService
	{
		private readonly PostgresContext _context;
		private readonly HttpClient _httpClient;
		private readonly IUserService _userService;
		private readonly WebHookRepository _webHookRepository;
		private readonly IAuthService _authService;

		public WebHookService(
			PostgresContext context, 
			HttpClient httpClient,
			IUserService userService,
			WebHookRepository webHookRepository,
			IAuthService authService
		)
		{
			_context = context;
			_httpClient = httpClient;
			_userService = userService;
			_webHookRepository = webHookRepository;
			_authService = authService;
		}

		public async Task<WebHook> CreateAsync(Guid userId, string url, long intents)
		{
			if (string.IsNullOrWhiteSpace(url))
				throw new ArgumentNullException("Provided URL is null or empty.");

			var webHook = new WebHook
			{
				UserId = userId,
				Url = url,
				Intents = intents,
			};

			_context.WebHooks.Add(webHook);

			await _context.SaveChangesAsync();

			return webHook;
		}

		public async Task<WebHook> EditAsync(Guid id, WebHook modifyData)
		{
			var webHook = await GetByWebHookIdAsync(id);

			if (webHook is null)
				throw new WebHookNotFoundException(id);

			webHook.MergeFrom(modifyData, w => w.Url, w => w.Intents);
			
			_context.WebHooks.Update(webHook);

			await _context.SaveChangesAsync();
			
			return webHook;
		}

		public async Task DeleteAsync(Guid id)
		{
			var webHook = await GetByWebHookIdAsync(id);

			if (webHook is null)
				throw new WebHookNotFoundException(id);

			_context.WebHooks.Remove(webHook);

			await _context.SaveChangesAsync();
		}

		public async Task SendMessageToAllAsync<T>(T message, IntentsFlags intents, string ContentType = "application/json")
		{
			var webhooks = GetWebHooks(intents)
				.Where(w => w.Status == WebHookStatus.Enabled);

			var content = WrapMessage(message, Encoding.UTF8, ContentType);
			await PostMessageToWebHooksAsync(content, webhooks);
		}

		public async Task SendMessageToUserAsync<T>(Guid userId, T message, IntentsFlags intents, string ContentType = "application/json")
		{
			var webhooks = GetByUserId(userId, intents)
				.Where(w => w.Status == WebHookStatus.Enabled);

			var content = WrapMessage(message, Encoding.UTF8, ContentType);
			await PostMessageToWebHooksAsync(content, webhooks);
		}

		public async Task SendMessageToRoleAsync<T>(string role, T message, IntentsFlags intents, string ContentType = "application/json")
		{
			var allWebhooks = GetWebHooks(intents)
				.Where(w => w.Status == WebHookStatus.Enabled);

			var webhooks = (await Task.WhenAll(
				allWebhooks.Select(async w => new 
				{
					WebHook = w,
					IsInRole = await _authService.IsInRoleAsync(w.UserId, role)
				})))
				.Where(x => x.IsInRole)
				.Select(x => x.WebHook)
				.ToList();

			var content = WrapMessage(message, Encoding.UTF8, ContentType);
			await PostMessageToWebHooksAsync(content, webhooks);
		}

		private string SerializeMessage<T>(T message) =>
			message is string str ? str : JsonSerializer.Serialize(message);

		private async Task PostMessageToWebHooksAsync(StringContent content, IEnumerable<WebHook> webhooks)
		{
			foreach (var webhook in webhooks)
				await _httpClient.PostAsync(webhook.Url, content);
		}

		private StringContent WrapMessage<T>(T message, Encoding encoding, string contentType) =>
			new StringContent(SerializeMessage(message), encoding, contentType);

		private IEnumerable<WebHook> GetWebHooks(long? intents, WebHookStatus? status = null, bool User = true) =>
			_webHookRepository.GetWebHooks(intents, status, User);

		private IEnumerable<WebHook> GetWebHooks(IntentsFlags? intents = null, WebHookStatus? status = null, bool User = true) =>
			_webHookRepository.GetWebHooks(intents, status, User);

		private async Task<WebHook?> GetByWebHookIdAsync(Guid id, IntentsFlags intents = IntentsFlags.All, bool User = true) =>
			await _webHookRepository.FindByIdAsync(id, intents, User);

		private IEnumerable<WebHook> GetByUserId(Guid userId, IntentsFlags intents = IntentsFlags.All, bool User = true) =>
			_webHookRepository.FindByUser(userId, intents, User);

		private async Task<WebHook?> GetByUrlAsync(string url, IntentsFlags intents = IntentsFlags.All, bool User = true) =>
			await _webHookRepository.FindByUrlAsync(url, intents, User);
	}
}
