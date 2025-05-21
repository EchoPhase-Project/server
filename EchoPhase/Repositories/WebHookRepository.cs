using Microsoft.EntityFrameworkCore;

using EchoPhase.DAL.Postgres;
using EchoPhase.Enums;
using EchoPhase.Processors.Enums;
using EchoPhase.Models;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;

namespace EchoPhase.Repositories
{
    public class WebHookRepository
	{
		private readonly PostgresContext _dbContext;

		public WebHookRepository(PostgresContext dbContext)
		{
			_dbContext = dbContext;
		}

		private IQueryable<WebHook> IncludeUserToQuery(IQueryable<WebHook> query) => query.Include(w => w.User);

		public IEnumerable<WebHook> GetWebHooks(
				long? intents = null, 
				WebHookStatus? status = null, 
				bool User = true)
		{
			IQueryable<WebHook> webHookQuery = _dbContext.WebHooks;

			if (User)
				webHookQuery = IncludeUserToQuery(webHookQuery);

			if (status != null)
				webHookQuery = webHookQuery.Where(w => w.Status == status);

			return ((intents is null) ? 
					webHookQuery : 
					webHookQuery
						.AsEnumerable()
						.Where(w => intents.HasIntents(w.Intents))
					).OrderByDescending(w => w.CreatedAt).ToList();
		}

		public IEnumerable<WebHook> GetWebHooks(
				IntentsFlags? intents = null, 
				WebHookStatus? status = null,
				bool User = true) =>
			GetWebHooks((long?)intents, status, User);

		public async Task<WebHook?> FindByIdAsync(Guid id, IntentsFlags intents = IntentsFlags.All, bool User = true)
		{
			IQueryable<WebHook> webHookQuery = _dbContext.WebHooks;

			if (User)
				webHookQuery = IncludeUserToQuery(webHookQuery);

			return await webHookQuery
						.FirstOrDefaultAsync(w => w.Id == id & intents.HasIntents(w.Intents));
		}

		public IEnumerable<WebHook> FindByUser(Guid userId, IntentsFlags intents = IntentsFlags.All, bool User = true)
		{
			IQueryable<WebHook> webHookQuery = _dbContext.WebHooks;

			if (User)
				webHookQuery = IncludeUserToQuery(webHookQuery);

			return webHookQuery
						.AsEnumerable()
						.Where(w => w.UserId == userId && intents.HasIntents(w.Intents))
						.ToList();
		}

		public async Task<WebHook?> FindByUrlAsync(string url, IntentsFlags intents = IntentsFlags.All, bool User = true)
		{
			IQueryable<WebHook> webHookQuery = _dbContext.WebHooks;

			if (User)
				webHookQuery = IncludeUserToQuery(webHookQuery);

			return await webHookQuery
						.FirstOrDefaultAsync(w => w.Url == url && intents.HasIntents(w.Intents));
		}
	}
}
