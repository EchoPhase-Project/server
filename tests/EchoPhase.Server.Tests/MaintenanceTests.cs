// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using System.Text.Json;
using EchoPhase.Cli.Commands.Maintenance;
using EchoPhase.DAL.Redis.Maintenance;
using EchoPhase.Security.BitMasks;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Middlewares;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace EchoPhase.Server.Tests
{
    public class MaintenanceTests
    {
        /// <summary>An in-memory store standing in for Redis, which can be made to fail.</summary>
        private sealed class Store : IMaintenanceStore
        {
            public MaintenanceState? State { get; set; }
            public TimeSpan? Expiry { get; private set; }
            public int Reads { get; private set; }
            public bool Fail { get; set; }

            public Task<MaintenanceState?> GetAsync(CancellationToken ct = default)
            {
                Reads++;
                return Fail ? Task.FromException<MaintenanceState?>(new IOException("Redis is down")) : Task.FromResult(State);
            }

            public Task SetAsync(MaintenanceState state, TimeSpan? expiry, CancellationToken ct = default)
            {
                (State, Expiry) = (state, expiry);
                return Task.CompletedTask;
            }

            public Task ClearAsync(CancellationToken ct = default)
            {
                State = null;
                return Task.CompletedTask;
            }
        }

        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero));
        private readonly Store _store = new();

        // The switch the middleware tests send through.
        private readonly MaintenanceSwitch _maintenance;

        public MaintenanceTests()
        {
            _maintenance = Switch();
        }

        private MaintenanceSwitch Switch() => new(_store, _time, NullLogger<MaintenanceSwitch>.Instance);

        [Fact]
        public async Task OnAndOff_AreSeenAtOnceHere_AndByOthersWithinTheCacheTime()
        {
            var here = Switch();
            var other = Switch();
            Assert.Null(await other.CurrentAsync(TestContext.Current.CancellationToken));

            await here.EnableAsync("Upgrading", TimeSpan.FromMinutes(30), TestContext.Current.CancellationToken);
            Assert.Equal("Upgrading", (await here.CurrentAsync(TestContext.Current.CancellationToken))?.Message);
            Assert.Null(await other.CurrentAsync(TestContext.Current.CancellationToken)); // still its cached "off"

            _time.Advance(MaintenanceSwitch.CacheFor);
            Assert.Equal("Upgrading", (await other.CurrentAsync(TestContext.Current.CancellationToken))?.Message);
            Assert.Equal(TimeSpan.FromMinutes(30), _store.Expiry);

            await here.DisableAsync(TestContext.Current.CancellationToken);
            Assert.Null(await here.CurrentAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Reads_AreCached()
        {
            var maintenance = Switch();
            for (int i = 0; i < 100; i++)
                await maintenance.CurrentAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, _store.Reads);
        }

        [Fact]
        public async Task ADuration_EndsItByItself_AndSetsRetryAfter()
        {
            var maintenance = Switch();
            var state = await maintenance.EnableAsync(null, TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);
            Assert.Null(state.Message);
            Assert.Equal(TimeSpan.FromMinutes(10), state.RetryAfter(_time.GetUtcNow()));

            _time.Advance(TimeSpan.FromMinutes(10));
            Assert.Null(await maintenance.CurrentAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task WithoutAnEnd_RetryAfterIsTheDefault()
        {
            var state = await Switch().EnableAsync("  ", null, TestContext.Current.CancellationToken);
            Assert.Null(state.Message);
            Assert.Null(state.Until);
            Assert.Equal(MaintenanceState.DefaultRetryAfter, state.RetryAfter(_time.GetUtcNow()));
            Assert.Null(_store.Expiry);
        }

        [Fact]
        public async Task WhenTheStoreFails_TheLastKnownStateStands_AndOffWhenNoneIsKnown()
        {
            var maintenance = Switch();
            _store.Fail = true;
            Assert.Null(await maintenance.CurrentAsync(TestContext.Current.CancellationToken)); // nothing known: open

            _store.Fail = false;
            await maintenance.EnableAsync("Upgrading", null, TestContext.Current.CancellationToken);
            _store.Fail = true;
            _time.Advance(MaintenanceSwitch.CacheFor);
            Assert.Equal("Upgrading", (await maintenance.CurrentAsync(TestContext.Current.CancellationToken))?.Message);
        }

        [Fact]
        public async Task ANonPositiveDuration_IsRefused()
            => await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Switch().EnableAsync(null, TimeSpan.Zero, TestContext.Current.CancellationToken));

        [Theory]
        [InlineData("90s", 90)]
        [InlineData("30m", 1800)]
        [InlineData("2H", 7200)]
        [InlineData("1d", 86400)]
        [InlineData("00:15:00", 900)]
        public void Durations_ReadAsTyped(string text, int seconds)
        {
            Assert.True(Durations.TryParse(text, out var duration));
            Assert.Equal(TimeSpan.FromSeconds(seconds), duration);
        }

        [Theory]
        [InlineData("")]
        [InlineData("0m")]
        [InlineData("-5m")]
        [InlineData("soon")]
        [InlineData("99999999999999d")]
        public void Durations_RefuseNonsense(string text)
            => Assert.False(Durations.TryParse(text, out _));

        [Fact]
        public void TheSwitch_IsForAdminsOnly_AndCannotBeGranted()
        {
            Assert.Contains(Permissions.Update, RolePermissions.CeilingFor([Roles.Admin], [])[Resources.Maintenance]);
            Assert.False(RolePermissions.CeilingFor([Roles.Staff], []).ContainsKey(Resources.Maintenance));
            Assert.False(RolePermissions.IsGrantable(Resources.Maintenance, Permissions.Update));
        }

        /// <summary>Authorizes the bypass policy only for users marked as switchers.</summary>
        private sealed class Authorization(bool allow) : IAuthorizationService
        {
            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
                => Task.FromResult(allow ? AuthorizationResult.Success() : AuthorizationResult.Failed());

            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
                => Task.FromResult(allow ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        private async Task<(HttpContext Context, bool Passed)> SendAsync(string path, bool signedIn = false, bool mayBypass = false)
        {
            var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
            context.Request.Path = path;
            if (signedIn)
                context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u")], "test"));

            bool passed = false;
            var middleware = new MaintenanceMiddleware(_ => { passed = true; return Task.CompletedTask; });
            await middleware.InvokeAsync(context, _maintenance, new Authorization(mayBypass), _time);
            return (context, passed);
        }

        private Task OnAsync(string? message, TimeSpan? duration) => _maintenance.EnableAsync(message, duration);

        [Fact]
        public async Task WhileOn_TheApiAnswers503_WithRetryAfterAndTheErrorBody()
        {
            await OnAsync("Upgrading the database", TimeSpan.FromMinutes(5));
            var (context, passed) = await SendAsync("/api/v1/workflows", signedIn: true);

            Assert.False(passed);
            Assert.Equal(503, context.Response.StatusCode);
            Assert.Equal("300", context.Response.Headers.RetryAfter.ToString());
            Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
            context.Response.Body.Position = 0;
            using var body = JsonDocument.Parse(context.Response.Body);
            Assert.Equal("Maintenance", body.RootElement.GetProperty("code").GetString());
            Assert.Equal("Upgrading the database", body.RootElement.GetProperty("message").GetString());
        }

        [Theory]
        [InlineData("/api/v1/status")]
        [InlineData("/api/v1/auth/login")]
        [InlineData("/api/v1/maintenance")]
        [InlineData("/health/ready")]
        [InlineData("/index.html")]
        public async Task WhileOn_StatusSigningInTheSwitchAndNonApiPaths_StayOpen(string path)
        {
            await OnAsync(null, null);
            Assert.True((await SendAsync(path)).Passed);
        }

        [Fact]
        public async Task WhileOn_TheSwitchersKeepUsingTheApi()
        {
            await OnAsync(null, null);
            Assert.True((await SendAsync("/api/v1/workflows", signedIn: true, mayBypass: true)).Passed);
            Assert.False((await SendAsync("/api/v1/gateway", signedIn: false, mayBypass: true)).Passed);
        }

        [Fact]
        public async Task WhileOff_EverythingPasses()
            => Assert.True((await SendAsync("/api/v1/workflows")).Passed);
    }
}
