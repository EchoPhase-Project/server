using System.Linq.Expressions;
using EchoPhase.DAL.Postgres;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.DAL.Postgres.Repositories.Options;
using EchoPhase.Security;
using EchoPhase.Services.Results;

namespace EchoPhase.Services
{
    public class DiscordTokenService : DataServiceBase<DiscordTokenRepository, DiscordTokenOptions>, IDiscordTokenService
    {
        private readonly PostgresContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IUserService _userService;
        private readonly AesService _aesService;

        public DiscordTokenService(
            PostgresContext context,
            IWebHostEnvironment environment,
            IUserService userService,
            AesService aesService,
            DiscordTokenRepository repository
        ) : base(repository)
        {
            _context = context;
            _environment = environment;
            _userService = userService;
            _aesService = aesService;
        }

        public IEnumerable<DiscordToken> Get(
            DiscordTokenSearchOptions opts,
            Func<IQueryable<DiscordToken>, DiscordTokenSearchOptions, IQueryable<DiscordToken>>? extraFilters = null
        )
        {
            return _repository.Get(opts, (query, opts) =>
                {
                    ExtraFilters(query, opts);

                    if (extraFilters != null)
                        query = extraFilters(query, opts);

                    return query;
                });
        }

        public IEnumerable<DiscordToken> Get(
            Action<DiscordTokenSearchOptions> configure,
            Func<IQueryable<DiscordToken>, DiscordTokenSearchOptions, IQueryable<DiscordToken>>? extraFilters = null
        )
        {
            return _repository.Get(configure, (query, opts) =>
                {
                    ExtraFilters(query, opts);

                    if (extraFilters != null)
                        query = extraFilters(query, opts);

                    return query;
                });
        }

        public async Task<IServiceResult> CreateAsync(DiscordToken token)
        {
            if (token.UserId != Guid.Empty && !_userService.UserExists(token.UserId))
                return ServiceResult.Failure(err =>
                    err.Set("NotFound", $"Provided UserId ${token.UserId} does not exist."));

            Encrypt(token);

            await _context.DiscordTokens.AddAsync(token);
            await _context.SaveChangesAsync();

            return ServiceResult.Success();
        }

        public async Task<IServiceResult<DiscordToken>> CreateAsync(Action<DiscordToken> configure)
        {
            var token = new DiscordToken();
            configure(token);

            var result = await CreateAsync(token);
            return result.To<DiscordToken>(token);
        }

        public async Task<IServiceResult<DiscordToken>> EditAsync(
            DiscordToken token,
            DiscordToken modifyData,
            params Expression<Func<DiscordToken, object>>[] overrideFields
        )
        {
            if (modifyData.UserId != Guid.Empty && !_userService.UserExists(modifyData.UserId))
                return ServiceResult<DiscordToken>.Failure(err =>
                    err.Set("NotFound", $"Provided UserId ${modifyData.UserId} does not exist."));

            token.MergeFrom(modifyData, overrideFields);
            Encrypt(token);

            _context.DiscordTokens.Update(token);
            await _context.SaveChangesAsync();

            return ServiceResult<DiscordToken>.Success(token);
        }

        public async Task<IServiceResult<DiscordToken>> EditAsync(
            DiscordToken token,
            Action<DiscordToken> configure,
            params Expression<Func<DiscordToken, object>>[] overrideFields
        )
        {
            var modifyData = new DiscordToken();
            configure(modifyData);

            return await EditAsync(token, modifyData, overrideFields);
        }

        public async Task<IServiceResult> DeleteAsync(DiscordToken token)
        {
            if (!_context.DiscordTokens.Contains(token))
                return ServiceResult.Failure(err =>
                    err.Set("NotFound", $"Token {token.Id} not found."));

            _context.DiscordTokens.Remove(token);
            await _context.SaveChangesAsync();

            return ServiceResult.Success();
        }

        private IQueryable<DiscordToken> ExtraFilters(
            IQueryable<DiscordToken> query,
            DiscordTokenSearchOptions opts
        )
        {
            query = query
                .AsEnumerable()
                .Select<DiscordToken, DiscordToken>(x =>
                {
                    Decrypt(x);
                    return x;
                })
                .AsQueryable();

            return query;
        }

        private void Encrypt(in DiscordToken token)
        {
            token.Token = _aesService.EncryptToBase64(token.Token);
        }

        private void Decrypt(in DiscordToken token)
        {
            token.Token = _aesService.DecryptToBase64(token.Token);
        }
    }
}
