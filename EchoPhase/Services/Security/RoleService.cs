using System.Security.Claims;

using Microsoft.AspNetCore.Identity;

using EchoPhase.Models;
using EchoPhase.Roles;
using EchoPhase.Interfaces;
using EchoPhase.Repositories;

namespace EchoPhase.Services.Security
{
	public class RoleService : IRoleService
	{
		private readonly RoleManager<UserRole> _roleManager;
		private readonly UserManager<User> _userManager;
		private readonly UserRepository _userRepository;

		public RoleService(
			RoleManager<UserRole> roleManager, 
			UserManager<User> userManager, 
			UserRepository userRepository
		)
		{
			_roleManager = roleManager;
			_userManager = userManager;
			_userRepository = userRepository;
		}

		public async Task CreateRoleAsync(string roleName)
		{
			if (await _roleManager.RoleExistsAsync(roleName))
				return;

			var role = new UserRole(roleName);
			var result = await _roleManager.CreateAsync(role);
			if (result.Succeeded)
				return;

			throw new Exception($"Failed to create role {roleName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
		}

		public async Task AddToRolesAsync(User user, params IEnumerable<string> roleNames)
		{
			foreach (var roleName in roleNames)
				await AddToRoleAsync(user, roleName);
		}

		public async Task AddToRoleAsync(User user, string roleName)
		{
			var role = await _roleManager.FindByNameAsync(roleName);
			if (role == null)
				throw new InvalidOperationException($"Role '{roleName}' not found.");

			if (await IsInRoleAsync(user, roleName))
				return;

			var result = await _userManager.AddToRoleAsync(user, roleName);
			if (result.Succeeded)
				return;

			throw new Exception($"Failed to add to role {roleName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
		}

		public async Task<bool> IsInRolesAsync(User user, params IEnumerable<string> roleNames)
		{
			foreach (var roleName in roleNames)
				if (!(await IsInRoleAsync(user, roleName)))
					return false;
			return true;
		}

		public async Task<bool> IsInRoleAsync(User user, string roleName) =>
			await _userManager.IsInRoleAsync(user, roleName);

		public bool IsInRoles(ClaimsPrincipal userClaims, params IEnumerable<string> roleNames)
		{
			foreach (var roleName in roleNames)
				if (!IsInRole(userClaims, roleName))
					return false;
			return true;
		}

		public bool IsInRole(ClaimsPrincipal userClaims, string roleName) =>
			userClaims.IsInRole(roleName);

		public async Task<IEnumerable<string>> GetRolesAsync(User user) =>
			await _userManager.GetRolesAsync(user);

		public async Task<IEnumerable<User>> GetUsersInRolesAsync(IEnumerable<string> roles)
		{
			var users = new HashSet<User>();

			foreach (var role in roles.Distinct())
			{
				var usersInRole = await _userManager.GetUsersInRoleAsync(role);
				foreach (var user in usersInRole)
				{
					users.Add(user);
				}
			}

			return users;
		}

		public async Task<IEnumerable<User>> GetUsersInRoleAsync(string role) =>
			await _userManager.GetUsersInRoleAsync(role);

		public IEnumerable<string> GetRoles(ClaimsPrincipal userClaims) =>
			userClaims.Claims
				.Where(c => c.Type == ClaimTypes.Role)
				.Select(c => c.Value);
	}
}
