// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EchoPhase.Configuration.Cryptography;
using EchoPhase.Security.Antiforgery;
using EchoPhase.Security.Cryptography.Vaults;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AesGcm = EchoPhase.Security.Cryptography.AesGcm;

namespace EchoPhase.Server.Tests
{
    /// <summary>A new tab of a signed-in session gets a CSRF token that works, and the other tabs keep theirs.</summary>
    public class AntiforgeryReissueTests
    {
        private static readonly AesGcm Aes = new(Options.Create(new CryptographyOptions()),
            new KeyRing(new ServiceCollection().AddSingleton<IKeyVault, FixedKeyVault>().BuildServiceProvider()));

        // HttpContextAccessor keeps one context per async flow (a static
        // AsyncLocal): several tabs in one test need one accessor each.
        private sealed class OwnContext : IHttpContextAccessor
        {
            public HttpContext? HttpContext { get; set; }
        }

        private sealed class Tab
        {
            public readonly OwnContext Accessor = new();
            public readonly AntiforgeryService Service;

            public Tab(string user, string? cookie = null, string? header = null)
            {
                var context = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, user)], "test")),
                };
                if (cookie is not null)
                    context.Request.Headers.Cookie = $"{AntiforgeryService.CookieName}={cookie}";
                if (header is not null)
                    context.Request.Headers[AntiforgeryService.HeaderName] = header;
                Accessor.HttpContext = context;
                Service = new AntiforgeryService(Aes, Accessor);
            }

            public HttpResponse Response => Accessor.HttpContext?.Response ?? throw new InvalidOperationException("No context.");

            public string Header => Response.Headers[AntiforgeryService.HeaderName].ToString();

            public string? SetCookie => Response.Headers.SetCookie.ToString() is { Length: > 0 } c
                ? c.Split(';')[0].Split('=', 2)[1]
                : null;
        }

        [Fact]
        public async Task ANewTab_GetsATokenForTheSameCookie_AndTheFirstTabsStillWorks()
        {
            // Sign-in: the first tab gets the cookie and its header token.
            var signIn = new Tab("u1");
            signIn.Service.Set();
            var cookie = Must.Present(signIn.SetCookie);
            var firstHeader = signIn.Header;

            // A second tab asks GET me: a header token, the cookie left alone.
            var second = new Tab("u1", cookie);
            second.Service.Reissue();
            Assert.Null(second.SetCookie);
            Assert.NotEqual(firstHeader, second.Header);

            // Both tabs' tokens pass with the one cookie.
            await new Tab("u1", cookie, firstHeader).Service.ValidateAsync();
            await new Tab("u1", cookie, second.Header).Service.ValidateAsync();
        }

        [Fact]
        public async Task WithoutACookie_OrAnotherUsersOne_AFreshPairIsSet()
        {
            var alone = new Tab("u1");
            alone.Service.Reissue();
            var cookie = Must.Present(alone.SetCookie);
            await new Tab("u1", cookie, alone.Header).Service.ValidateAsync();

            var other = new Tab("u2", cookie);
            other.Service.Reissue();
            var otherCookie = Must.Present(other.SetCookie);
            await new Tab("u2", otherCookie, other.Header).Service.ValidateAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => new Tab("u2", cookie, other.Header).Service.ValidateAsync());
        }
    }
}
