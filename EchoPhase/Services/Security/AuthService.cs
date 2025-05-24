using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

using EchoPhase.DAL.Redis;
using EchoPhase.DAL.Postgres;
using EchoPhase.Funcs;
using EchoPhase.Roles;
using EchoPhase.Models;
using EchoPhase.Repositories;
using EchoPhase.Interfaces;

namespace EchoPhase.Services.Security
{
    public class AuthService : IAuthService
	{
		private readonly PostgresContext _context;
		private readonly UserRepository _userRepository;
		private readonly UserManager<User> _userManager;	
		private readonly RoleService _roleService;
		private readonly SignInManager<User> _signInManager;
		private readonly ITokenService _jwtTokenService;
		private readonly IAuthorizationService _authorizationService;
		private readonly IUserService _userService;
		private readonly ICacheContext _cacheContext;

		public AuthService(
				PostgresContext context, 
				UserRepository userRepository, 
				UserManager<User> userManager, 
				RoleService roleService, 
				SignInManager<User> signInManager, 
				ITokenService jwtTokenService, 
				IAuthorizationService authorizationService, 
				IUserService userService,
				ICacheContext cacheContext)
		{
			_context = context;
			_userRepository = userRepository;
			_userManager = userManager;
			_roleService = roleService;
			_signInManager = signInManager;
			_jwtTokenService = jwtTokenService;
			_authorizationService = authorizationService;
			_userService = userService;
			_cacheContext = cacheContext;
		}

		public async Task<IdentityResult> CreateUserAsync(string name, string username, string password)
		{
			var user = new User(name) {
				UserName = username
			};

			var result = await _userManager.CreateAsync(user, password);

			if (result.Succeeded)
			{
				await _roleService.AssignRoleToUserAsync(user, "User");
				var signInResult = await AuthenticateAsync(username, password);
			}

			return result;
		}

		public async Task<SignInResult> AuthenticateAsync(string username, string password)
		{
			return await _signInManager.PasswordSignInAsync(username, password, false, lockoutOnFailure: false);
		}

		public async Task<bool> LogoutAsync()
		{
			try
			{
				await _signInManager.SignOutAsync();
				return true;
			}
			catch
			{
				return false;
			}
		}

		public bool IsAuthenticated(ClaimsPrincipal user)
		{
			return _signInManager.IsSignedIn(user);
		}

		public async Task<bool> IsInPolicyAsync(ClaimsPrincipal? userPrincipal, string policyName)
		{
			if (userPrincipal is null)
				return false;

			AuthorizationResult result = await _authorizationService.AuthorizeAsync(userPrincipal, policyName);
			return result.Succeeded;
		}

		public async Task<bool> IsInRoleAsync(User user, string roleName)
		{
			bool result = await _roleService.IsInRoleAsync(user.Id, roleName);
			return result;
		}

		public async Task<bool> IsInRoleAsync(ClaimsPrincipal userPrincipal, string roleName)
		{
			User? user = await _userService.GetUserAsync(userPrincipal);
			if (user is null)
				throw new NullReferenceException("User not found.");

			return await IsInRoleAsync(user, roleName);
		}

		public async Task<bool> IsInRoleAsync(Guid userId, string roleName)
		{
			if (userId == Guid.Empty)
				throw new ArgumentNullException("Empty GUID.");

			User? user = await _userService.GetUserAsync(userId);
			if (user is null)
				throw new NullReferenceException("User not found.");

			return await IsInRoleAsync(user, roleName);
		}

		public async Task<string> GenerateTokenAsync(User user) =>
			await _jwtTokenService.GenerateTokenAsync(user);

		public bool IsTokenValid(string token) =>
			_jwtTokenService.ValidateToken(token) != null;

		public void RevokeToken(string token) =>
			_jwtTokenService.RevokeToken(token);

		public void ExtendToken(string token) =>
			_jwtTokenService.ExtendToken(token);
	}
}

namespace EchoPhase.Interfaces
{
	public interface IAuthService
	{
		Task<IdentityResult> CreateUserAsync(string name, string username, string password);
		Task<SignInResult> AuthenticateAsync(string username, string password);
		Task<bool> LogoutAsync();
		bool IsAuthenticated(ClaimsPrincipal user);
		bool IsTokenValid(string token);
		Task<bool> IsInPolicyAsync(ClaimsPrincipal userPrincipal, string policyName);
		Task<bool> IsInRoleAsync(User user, string roleName);
		Task<bool> IsInRoleAsync(ClaimsPrincipal userPrincipal, string roleName);
		Task<bool> IsInRoleAsync(Guid userId, string roleName);
		Task<string> GenerateTokenAsync(User user);
		void RevokeToken(string token);
		void ExtendToken(string token);
	}
}
