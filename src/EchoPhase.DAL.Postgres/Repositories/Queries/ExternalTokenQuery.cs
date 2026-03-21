// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;
using EchoPhase.Security.Cryptography;
using EchoPhase.Types.Repository;
using Microsoft.EntityFrameworkCore;

// ── ExternalTokenQuery ────────────────────────────────────────────────────────

namespace EchoPhase.DAL.Postgres.Repositories.Queries
{
    public class ExternalTokenQuery : RepositoryQuery<ExternalToken>
    {
        private readonly AesGcm _aes;

        public ExternalTokenQuery(IQueryable<ExternalToken> query, AesGcm aes)
            : base(query)
        {
            _aes = aes;
        }

        public ExternalTokenQuery WithIds(params Guid[] ids)
        {
            Where(x => ids.Contains(x.Id));
            return this;
        }

        public ExternalTokenQuery WithUserIds(params Guid[] userIds)
        {
            Where(x => userIds.Contains(x.UserId));
            return this;
        }

        public ExternalTokenQuery WithProviderNames(params string[] names)
        {
            Where(x => names.Contains(x.ProviderName));
            return this;
        }

        public ExternalTokenQuery WithTokenNames(params string[] names)
        {
            Where(x => names.Contains(x.TokenName));
            return this;
        }

        public ExternalTokenQuery WithUser()
        {
            Include(x => x.User);
            return this;
        }

        public override ExternalToken? FirstOrDefault()
        {
            var token = base.FirstOrDefault();
            token?.Value = _aes.Decrypt(token.Value);
            return token;
        }

        public override List<ExternalToken> ToList()
        {
            var tokens = base.ToList();
            foreach (var token in tokens)
                token.Value = _aes.Decrypt(token.Value);
            return tokens;
        }

        public override CursorPage<ExternalToken> ToPage()
        {
            var page = base.ToPage();
            foreach (var token in page.Data)
                token.Value = _aes.Decrypt(token.Value);
            return page;
        }
    }
}
