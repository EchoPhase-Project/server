using System.Linq.Expressions;
using EchoPhase.DAL.Postgres;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;
using EchoPhase.Models;
using EchoPhase.Repositories;
using EchoPhase.Repositories.Options;
using EchoPhase.Services.Results;
using EchoPhase.Services.Security;

namespace EchoPhase.Services
{
    public class DiscordTokenService : DataServiceBase<DiscordTokenService, DiscordTokenRepository, DiscordTokenOptions>, IDiscordTokenService
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

        public async Task<DiscordToken> CreateAsync(DiscordToken token)
        {
            if (token.UserId != Guid.Empty && !_userService.UserExists(token.UserId))
                throw new InvalidOperationException($"Provided UserId ${token.UserId} does not exist.");

            Encrypt(token);
            await _context.DiscordTokens.AddAsync(token);

            await _context.SaveChangesAsync();

            return token;
        }

        public async Task<IDiscordTokenResult> CreateAsync(params IEnumerable<DiscordToken> tokens)
        {
            var result = new DiscordTokenResult();
            foreach (var token in tokens)
            {
                try
                {
                    result.Affected.Add(await CreateAsync(token));
                }
                catch (Exception e)
                {
                    result.Errors.Add(e.Message);
                    if (result.IsSucceeded)
                        result.IsSucceeded = false;
                }
            }

            return result;
        }

        public async Task<DiscordToken> EditAsync(
            DiscordToken token,
            DiscordToken modifyData,
            params Expression<Func<DiscordToken, object>>[] overrideFields
        )
        {
            if (modifyData.UserId != Guid.Empty && !_userService.UserExists(modifyData.UserId))
                throw new InvalidOperationException($"Provided UserId ${modifyData.UserId} does not exist.");

            Encrypt(token);
            token.MergeFrom(modifyData, overrideFields);
            _context.DiscordTokens.Update(token);

            await _context.SaveChangesAsync();

            return token;
        }

        public async Task<IDiscordTokenResult> EditAsync(
            IEnumerable<DiscordToken> tokens,
            DiscordToken modifyData,
            params Expression<Func<DiscordToken, object>>[] overrideFields
        )
        {
            var result = new DiscordTokenResult();
            foreach (var token in tokens)
            {
                try
                {
                    result.Affected.Add(await EditAsync(token, modifyData, overrideFields));
                }
                catch (Exception e)
                {
                    result.Errors.Add(e.Message);
                    if (result.IsSucceeded)
                        result.IsSucceeded = false;
                }
            }

            return result;
        }

        public async Task<DiscordToken> DeleteAsync(DiscordToken token)
        {
            if (!_context.DiscordTokens.Contains(token))
                throw new InvalidOperationException($"Token {token.Id} not found.");

            _context.DiscordTokens.Remove(token);
            await _context.SaveChangesAsync();

            return token;
        }

        public async Task<IDiscordTokenResult> DeleteAsync(params IEnumerable<DiscordToken> tokens)
        {
            var result = new DiscordTokenResult();
            foreach (var token in tokens)
            {
                try
                {
                    result.Affected.Add(await DeleteAsync(token));
                }
                catch (Exception e)
                {
                    result.Errors.Add(e.Message);
                    if (result.IsSucceeded)
                        result.IsSucceeded = false;
                }
            }

            return result;
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
            token.Token = _aesService.Encrypt(token.Token);
        }

        private void Decrypt(in DiscordToken token)
        {
            token.Token = _aesService.Decrypt(token.Token);
        }
    }
}
