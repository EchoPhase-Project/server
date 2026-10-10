// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using EchoPhase.DAL.Abstractions.Service;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.DAL.Redis.Interfaces;
using EchoPhase.DAL.Redis.Models;
using EchoPhase.Security.Hashers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;

namespace EchoPhase.Identity
{
    public class UserService : DataServiceBase<User, UserRepository>, IUserService
    {
        // Same bound as registration (RegisterRequest.Name).
        private const int MinNameLength = 2;

        private readonly PostgresContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly ICacheContext _cacheContext;
        private readonly IRoleService _roleService;
        private readonly PasswordHashingGate _hashing;

        public UserService(
            PostgresContext context,
            UserRepository repository,
            UserManager<User> userManager,
            PasswordHashingGate hashing,
            IWebHostEnvironment environment,
            ICacheContext cacheContext,
            IRoleService roleService
        ) : base(repository)
        {
            _context = context;
            _userManager = userManager;
            _hashing = hashing;
            _environment = environment;
            _cacheContext = cacheContext;
            _roleService = roleService;
        }

        public async Task<IdentityResult> CreateUserAsync(
            string name,
            string username,
            string password,
            string? timeZone = null,
            params string[] roles)
        {
            var user = new User(name) { UserName = username };

            // CreateAsync checks the password first and stops there, so a weak
            // password would hide a taken username. Report every problem at once.
            var problems = await ValidateAsync(user, password);
            if (timeZone is not null)
            {
                if (IsIanaTimeZone(timeZone))
                    user.TimeZone = timeZone;
                else
                    problems.Add(InvalidTimeZone(timeZone));
            }
            if (problems.Count > 0)
                return IdentityResult.Failed(problems.ToArray());

            var result = await _hashing.RunAsync(() => _userManager.CreateAsync(user, password));

            if (result.Succeeded)
                await _roleService.AddToRolesAsync(user, roles);

            return result;
        }

        private static IdentityError InvalidTimeZone(string timeZone) => new()
        {
            Code = "InvalidTimeZone",
            Description = $"'{timeZone}' is not an IANA time zone, e.g. 'Europe/Moscow'."
        };

        // Known to this host's tz database and in IANA form: on Linux .NET
        // would also accept Windows names ("Russian Standard Time").
        private static bool IsIanaTimeZone(string id)
            => id.Length <= 64
               && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)
               && zone.HasIanaId;

        private async Task<List<IdentityError>> ValidateAsync(User user, string password)
        {
            var problems = new List<IdentityError>();

            foreach (var validator in _userManager.UserValidators)
                problems.AddRange((await validator.ValidateAsync(_userManager, user)).Errors);

            foreach (var validator in _userManager.PasswordValidators)
                problems.AddRange((await validator.ValidateAsync(_userManager, user, password)).Errors);

            return problems;
        }

        public async Task<IdentityResult> UpdateProfileAsync(User user, string? name, string? timeZone)
        {
            var problems = new List<IdentityError>();
            if (name is not null && name.Trim().Length < MinNameLength)
                problems.Add(new IdentityError
                {
                    Code = "InvalidName",
                    Description = $"Name must be at least {MinNameLength} characters."
                });
            if (timeZone is not null && !IsIanaTimeZone(timeZone))
                problems.Add(InvalidTimeZone(timeZone));
            if (problems.Count > 0)
                return IdentityResult.Failed(problems.ToArray());

            if (name is not null)
                user.Name = name.Trim();
            if (timeZone is not null)
                user.TimeZone = timeZone;

            return await _userManager.UpdateAsync(user);
        }

        public Task<IdentityResult> DeleteUserAsync(User user)
            => _userManager.DeleteAsync(user);

        public async Task<User?> GetAsync(ClaimsPrincipal principal)
        {
            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (sub is null || !Guid.TryParse(sub, out var userId))
                return null;

            return await _userManager.FindByIdAsync(userId.ToString());
        }

        public async Task<string> GetOrSetCodeAsync(User user)
        {
            var duration = TimeSpan.FromMinutes(20);

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
                    throw new InvalidOperationException("Invalid cache state.");
            }
        }

        public async Task<IDictionary<Guid, string>> GetOrSetCodesAsync(params IEnumerable<User> users)
        {
            IDictionary<Guid, string> codes = new Dictionary<Guid, string>();

            foreach (var user in users)
            {
                codes[user.Id] = await GetOrSetCodeAsync(user);
            }

            return codes;
        }

        private string GenerateRandomCode()
        {
            var bytes = new byte[12];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        public async Task<Guid> GetIdFromCode(string code) =>
            (await _cacheContext.Entry<QrCache>(code).GetAsync()).UserId;

        public async Task<IDictionary<string, Guid>> GetIdsFromCodes(params IEnumerable<string> codes)
        {
            IDictionary<string, Guid> dict = new Dictionary<string, Guid>();
            foreach (var code in codes)
            {
                Guid userId = await GetIdFromCode(code);
                dict[code] = userId;
            }

            return dict;
        }

        public bool UserExists(Guid id) =>
            _context.Users.Any(e => e.Id == id);

        public bool UserExists(string username) =>
            _context.Users.Any(e => e.UserName == username);

        public string GetProfileImagePath(Guid userId, string filename, bool root = false)
        {
            if (userId == Guid.Empty)
                throw new ArgumentNullException(nameof(userId));

            string result = String.IsNullOrEmpty(filename)
                ? Path.Combine("files", userId.ToString(), "image")
                : Path.Combine("files", userId.ToString(), "image", filename);

            return root
                ? Path.Combine(_environment.ContentRootPath, result)
                : result;
        }

        public async Task<IdentityResult> UpdateSecurityStampAsync(User user)
        {
            await _cacheContext.Entry<SecurityStamp>(user.Id.ToString()).RemoveAsync();
            return await _userManager.UpdateSecurityStampAsync(user);
        }
    }
}
