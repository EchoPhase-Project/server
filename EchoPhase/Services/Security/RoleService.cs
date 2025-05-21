using Microsoft.AspNetCore.Identity;

using EchoPhase.Models;
using EchoPhase.Roles;
using EchoPhase.Repositories;
using EchoPhase.Interfaces;

namespace EchoPhase.Services.Security
{
	public class RoleService
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
			if (!await _roleManager.RoleExistsAsync(roleName))
			{
				var role = new UserRole(roleName);
				await _roleManager.CreateAsync(role);
			}
		}

		public async Task AssignRoleToUserAsync(User user, string roleName)
		{
			if (user == null)
				throw new ApplicationException($"User not found.");

			var role = await _roleManager.FindByNameAsync(roleName);
			if (role == null)
				throw new ApplicationException($"Role '{roleName}' not found.");

			if (!await _userManager.IsInRoleAsync(user, roleName))
				await _userManager.AddToRoleAsync(user, roleName);
		}

		public async Task AssignRoleToUserAsync(Guid userId, string roleName)
		{
			var user = await _userRepository.FindByIdAsync(userId);
			if (user == null)
				throw new ApplicationException($"User with ID '{userId}' not found.");

			await this.AssignRoleToUserAsync(user, roleName);
		}

		public async Task<bool> IsInRoleAsync(Guid userId, string roleName)
		{
			var user = await _userRepository.FindByIdAsync(userId);
			if (user == null)
				throw new ApplicationException($"User with ID '{userId}' not found.");

			return await _userManager.IsInRoleAsync(user, roleName);
		}

		public async Task<List<string>> GetRolesAsync(User user)
		{
			if (user == null)
				throw new ApplicationException($"No user provided.");

			var roles = await _userManager.GetRolesAsync(user);

			return roles.ToList();
		}

		public async Task<List<string>> GetRolesAsync(Guid userId)
		{
			var user = await _userRepository.FindByIdAsync(userId);
			if (user == null)
				throw new ApplicationException($"User with ID '{userId}' not found.");

			var roles = await this.GetRolesAsync(user);

			return roles;
		}

	}
}
