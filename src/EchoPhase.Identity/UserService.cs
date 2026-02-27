using System.Security.Claims;
using System.Security.Cryptography;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.DAL.Redis.Interfaces;
using EchoPhase.DAL.Redis.Models;
using EchoPhase.Types.Service;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using UserOptions = EchoPhase.DAL.Postgres.Repositories.Options.UserOptions;

namespace EchoPhase.Identity
{
    public class UserService : DataServiceBase<User, UserRepository, UserOptions>, IUserService
    {
        private readonly PostgresContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly ICacheContext _cacheContext;

        public UserService(
            PostgresContext context,
            UserRepository repository,
            UserManager<User> userManager,
            IWebHostEnvironment environment,
            ICacheContext cacheContext
        ) : base(repository)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
            _cacheContext = cacheContext;
        }

        public IEnumerable<User> Get(
            UserSearchOptions opts,
            Func<IQueryable<User>, UserSearchOptions, IQueryable<User>>? extraFilters = null
        )
        {
            return _repository.Get(opts, extraFilters);
        }

        public IEnumerable<User> Get(
            Action<UserSearchOptions> configure,
            Func<IQueryable<User>, UserSearchOptions, IQueryable<User>>? extraFilters = null
        )
        {
            return _repository.Get(configure, extraFilters);
        }

        public async Task<User> GetAsync(ClaimsPrincipal userPrincipal) =>
            await _userManager.GetUserAsync(userPrincipal) ??
                throw new InvalidOperationException("User not found.");

        public async Task<string> GetOrSetCodeAsync(User user)
        {
            TimeSpan duration = TimeSpan.FromMinutes(20);

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

            string result = (String.IsNullOrEmpty(filename))
                ? Path.Combine("files", userId.ToString(), "image")
                : Path.Combine("files", userId.ToString(), "image", filename);

            return (root)
                ? Path.Combine(_environment.ContentRootPath, result)
                : result;
        }
    }
}
