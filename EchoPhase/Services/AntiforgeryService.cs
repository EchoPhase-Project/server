using Microsoft.AspNetCore.Antiforgery;

using EchoPhase.Interfaces;

namespace EchoPhase.Services
{
	public class AntiforgeryService : IAntiforgeryService
	{
		private readonly IAntiforgery _antiforgery;
		private readonly IHttpContextAccessor _httpContextAccessor;

		public AntiforgeryService(IAntiforgery antiforgery, IHttpContextAccessor httpContextAccessor)
		{
			_antiforgery = antiforgery;
			_httpContextAccessor = httpContextAccessor;
		}

		public bool SetAntiforgeryToken()
		{
			if (_httpContextAccessor.HttpContext is null)
				return false;
			var token = _antiforgery.GetAndStoreTokens(_httpContextAccessor.HttpContext);
			_httpContextAccessor.HttpContext.Response.Headers["X-CSRF-TOKEN"] = token.RequestToken;
			return true;
		}

		public string? GetAntiforgeryToken()
		{
			if (_httpContextAccessor.HttpContext is null)
				return null;
			var tokens = _antiforgery.GetAndStoreTokens(_httpContextAccessor.HttpContext);
			return tokens.RequestToken;
		}

		public async Task<bool> ValidateAntiforgeryTokenAsync()
		{
			try
			{
				if (_httpContextAccessor.HttpContext is null)
					return false;

				await _antiforgery.ValidateRequestAsync(_httpContextAccessor.HttpContext);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
