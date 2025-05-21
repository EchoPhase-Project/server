using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Redis.Models;
using EchoPhase.Funcs;
using EchoPhase.Models;
using EchoPhase.Repositories;
using EchoPhase.Interfaces;
using EchoPhase.Services.Security;

namespace EchoPhase.Services
{
    public class UserService : IUserService
	{
		private readonly PostgresContext _context;
		private readonly UserRepository _userRepository;
		private readonly UserManager<User> _userManager;	
		private readonly RoleService _roleService;
		private readonly IWebHostEnvironment _environment;
		private readonly ICacheContext _cacheContext;

		public UserService(
				PostgresContext context, 
				UserRepository userRepository, 
				UserManager<User> userManager, 
				RoleService roleService, 
				IWebHostEnvironment environment,
				ICacheContext cacheContext)
		{
			_context = context;
			_userRepository = userRepository;
			_userManager = userManager;
			_roleService = roleService;
			_environment = environment;
			_cacheContext = cacheContext;
		}

		public async Task<List<User>> GetUsersAsync()
		{
			return await _userRepository.GetUsersAsync();
		}

		public async Task<List<User>> GetUsersAsync(Guid userId)
		{
			return await _userRepository.GetUsersAsync(userId);
		}

		public async Task<User> GetUserAsync(Guid userId)
		{
			if (userId == Guid.Empty)
				throw new ArgumentNullException("Empy GUID.");

			User? user = await _userRepository.FindByIdAsync(userId);
			if (user is null)
				throw new NullReferenceException("User not found.");

			return user;
		}

		public async Task<User> GetUserAsync(string userName)
		{
			User? user = await _userRepository.FindByUserNameAsync(userName);
			if (user is null)
				throw new NullReferenceException("User not found.");

			return user;
		}

		public async Task<User> GetUserAsync(ClaimsPrincipal userPrincipal)
		{
			User? user = await _userManager.GetUserAsync(userPrincipal);
			if (user is null)
				throw new NullReferenceException("User not found.");

			return user;
		}


		public async Task<string> GetOrSetCodeAsync(User user)
		{
			TimeSpan duration = TimeSpan.FromMinutes(20);

			while (true)
			{
				QrUserCache qrUserCache = await _cacheContext
					.Entry<QrUserCache>(user.Id.ToString())
					.GetOrSetAsync(
						() => new QrUserCache { Code = GenerateRandomCode() }, 
						duration);

				QrCache qrCache = await _cacheContext.
					Entry<QrCache>(qrUserCache.Code)
					.GetAsync();

				switch (qrCache.UserId)
				{
					case var _ when qrCache.UserId == Guid.Empty:
						await _cacheContext.
							Entry<QrCache>(qrUserCache.Code)
							.SetAsync(
								new QrCache { UserId = user.Id }, 
								duration);
						return qrUserCache.Code;

					case var _ when qrCache.UserId == user.Id:
						return qrUserCache.Code;

					default:
						await _cacheContext.
							Entry<QrCache>(user.Id.ToString()).
							RemoveAsync();
						break;
				}
			}
		}

		private string GenerateRandomCode()
		{
			const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
			var random = new Random();
			return new string(Enumerable.Repeat(chars, 16)
				.Select(s => s[random.Next(s.Length)]).ToArray());
		}

		public async Task<Guid> GetUserIdFromCode(string code) =>
			(await _cacheContext.Entry<QrCache>(code).GetAsync()).UserId;

		public async Task<User> GetUserFromCode(string code) =>
			await GetUserAsync(
					await GetUserIdFromCode(code));

		public async Task<IdentityResult> UpdateUserAsync(User user) =>
			await _userManager.UpdateAsync(user);

        public bool UserExists(Guid id) =>
            _context.Users.Any(e => e.Id == id);

		public async Task<string> GetOrSetCodeAsync(Guid userId) =>
			await GetOrSetCodeAsync(
				await GetUserAsync(userId));

		public async Task<string> GetOrSetCodeAsync(ClaimsPrincipal userPrincipal) =>
			await this.GetOrSetCodeAsync(
				await GetUserAsync(userPrincipal));

		public string GetProfileImagePath(Guid userId, string filename, bool root = false)
		{
			if (userId == Guid.Empty)
				throw new ArgumentNullException(nameof(userId));

			string result = (String.IsNullOrEmpty(filename))
				? Path.Combine("files", userId.ToString(), "image")
				: Path.Combine("files", userId.ToString(), "image", filename);

			return (root)
				? Path.Combine(_environment.ContentRootPath, result)
				: result;
		}
	}
}

namespace EchoPhase.Interfaces
{
	public interface IUserService
	{
		Task<List<User>> GetUsersAsync();
		Task<List<User>> GetUsersAsync(Guid userId);
		Task<User> GetUserAsync(ClaimsPrincipal userPrincipal);
		Task<User> GetUserAsync(Guid userId);
		Task<User> GetUserAsync(string UserName);
		Task<string> GetOrSetCodeAsync(ClaimsPrincipal userPrincipal);
		Task<string> GetOrSetCodeAsync(Guid userId);
		Task<string> GetOrSetCodeAsync(User user);
		Task<Guid> GetUserIdFromCode(string code);
		Task<User> GetUserFromCode(string code);
		Task<IdentityResult> UpdateUserAsync(User user);
        bool UserExists(Guid id);
		string GetProfileImagePath(Guid userId, string filename, bool root = false);
	}
}
