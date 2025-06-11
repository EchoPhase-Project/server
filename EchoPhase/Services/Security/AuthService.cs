using System.Security.Claims;
using EchoPhase.DAL.Postgres;
using EchoPhase.Interfaces;
using EchoPhase.Models;
using EchoPhase.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace EchoPhase.Services.Security
{
    public class AuthService : IAuthService
    {
        private readonly PostgresContext _context;
        private readonly UserRepository _userRepository;
        private readonly UserManager<User> _userManager;
        private readonly IRoleService _roleService;
        private readonly SignInManager<User> _signInManager;
        private readonly IAuthorizationService _authService;
        private readonly IUserService _userService;

        public AuthService(
            PostgresContext context,
            UserRepository userRepository,
            UserManager<User> userManager,
            IRoleService roleService,
            SignInManager<User> signInManager,
            IAuthorizationService authService,
            IUserService userService
        )
        {
            _context = context;
            _userRepository = userRepository;
            _userManager = userManager;
            _roleService = roleService;
            _signInManager = signInManager;
            _authService = authService;
            _userService = userService;
        }

        public async Task<IdentityResult> CreateUserAsync(string name, string username, string password)
        {
            var user = new User(name)
            {
                UserName = username
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _roleService.AddToRolesAsync(user, "User");
                var signInResult = await AuthenticateAsync(username, password);
            }

            return result;
        }

        public async Task<SignInResult> AuthenticateAsync(string username, string password) =>
            await _signInManager.PasswordSignInAsync(username, password, false, lockoutOnFailure: false);

        public async Task LogoutAsync() =>
            await _signInManager.SignOutAsync();

        public bool IsAuthenticated(ClaimsPrincipal user) =>
            _signInManager.IsSignedIn(user);

        public async Task<bool> IsInPoliciesAsync(ClaimsPrincipal userPrincipal, IEnumerable<string> policiesName)
        {
            foreach (var policyName in policiesName)
                if (!(await IsInPolicyAsync(userPrincipal, policyName)))
                    return false;
            return true;
        }

        public async Task<bool> IsInPolicyAsync(ClaimsPrincipal userPrincipal, string policyName)
        {
            var result = await _authService.AuthorizeAsync(userPrincipal, policyName);
            if (result.Succeeded)
                return true;

            throw new Exception($"Failed to check policy {policyName}: {result.Failure}");
        }
    }
}
