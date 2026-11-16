// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Claims;
using EchoPhase.Configuration.Argon2;
using EchoPhase.Configuration.Authentication;
using EchoPhase.Configuration.Authentication.Bearer;
using EchoPhase.DAL.Postgres;
using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.Authentication.Jwt;
using EchoPhase.Security.Authentication.Jwt.Claims;
using EchoPhase.Security.Authentication.Jwt.Providers;
using EchoPhase.Security.Authentication.Jwt.Verdicts;
using EchoPhase.Security.Authentication.Secrets;
using EchoPhase.Security.BitMasks;
using EchoPhase.Security.Cryptography.Vaults;
using EchoPhase.Security.Hashers;
using EchoPhase.Server.Services.Realtime;
using EchoPhase.Types.Claims;
using EchoPhase.WebSockets;
using EchoPhase.WebSockets.Cluster;
using EchoPhase.WebSockets.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using GatewayOptions = EchoPhase.Configuration.WebSocket.WebSocketOptions;

namespace EchoPhase.Server.Tests
{
    /// <summary>
    /// A gateway connection lives as long as the session it was opened with:
    /// signing out, revoking the session from another device, signing out
    /// everywhere or a replayed refresh token close it at once, on every
    /// replica, with <see cref="CloseCodes.SessionEnded"/>. The user's other
    /// sessions stay connected. Real session, token and gateway services on
    /// an in-memory database; two replicas share an in-memory backplane.
    /// </summary>
    public sealed class SessionGatewayCloseTests : IAsyncDisposable
    {
        private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
        private readonly InMemoryDatabaseRoot _database = new();
        private readonly string _databaseName = Guid.NewGuid().ToString();
        private readonly MemoryVerdicts _verdicts = new();
        private readonly Bus _bus = new();
        private readonly Replica _a;
        private readonly Replica _b;
        private readonly User _alice;

        public SessionGatewayCloseTests()
        {
            _a = new Replica(Build("a"));
            _b = new Replica(Build("b"));

            _alice = new User("Alice") { UserName = "alice", SecurityStamp = Guid.NewGuid().ToString() };
            using var scope = _a.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PostgresContext>();
            db.Users.Add(_alice);
            db.SaveChanges();
        }

        public async ValueTask DisposeAsync()
        {
            await _a.Services.DisposeAsync();
            await _b.Services.DisposeAsync();
        }

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task SigningOut_ClosesThatSessionsSocket_AtOnce_AndNoOther()
        {
            var phone = await SignInAsync("phone");
            var laptop = await SignInAsync("laptop");
            var phoneSocket = _a.Connect(phone.Principal);
            var laptopSocket = _a.Connect(laptop.Principal);

            await _a.UseAsync<RefreshSignInManager>(m => m.LogoutAsync(_alice.Id, phone.RefreshToken, Ct));

            Assert.Equal(CloseCodes.SessionEnded, phoneSocket.ClosedWith);
            Assert.Null(laptopSocket.ClosedWith);

            // The other session's token stays good: the monitor leaves it open.
            await _a.RevalidateAsync();
            Assert.Null(laptopSocket.ClosedWith);
            Assert.Equal(PrincipalValidity.SessionEnded, await _a.RevalidateAsync(phone.Principal));
            Assert.Equal(PrincipalValidity.Valid, await _a.RevalidateAsync(laptop.Principal));
        }

        [Fact]
        public async Task RevokingASession_FromAnotherDevice_ClosesItsSocket()
        {
            var phone = await SignInAsync("phone");
            var laptop = await SignInAsync("laptop");
            var phoneSocket = _a.Connect(phone.Principal);
            var laptopSocket = _a.Connect(laptop.Principal);

            // DELETE /sessions/{id}, sent from the laptop.
            await _a.UseAsync<RefreshSignInManager>(m => m.RevokeSessionAsync(_alice.Id, phone.SessionId, Ct));

            Assert.Equal(CloseCodes.SessionEnded, phoneSocket.ClosedWith);
            Assert.Null(laptopSocket.ClosedWith);
            await _a.RevalidateAsync();
            Assert.Null(laptopSocket.ClosedWith);
        }

        [Fact]
        public async Task ASocketOnAnotherReplica_IsClosedThroughTheBackplane()
        {
            var phone = await SignInAsync("phone");
            var laptop = await SignInAsync("laptop");
            var phoneSocket = _b.Connect(phone.Principal);
            var laptopSocket = _b.Connect(laptop.Principal);

            // The sign-out request lands on replica a; the socket lives on b.
            await _a.UseAsync<RefreshSignInManager>(m => m.LogoutAsync(_alice.Id, phone.RefreshToken, Ct));

            Assert.Equal(CloseCodes.SessionEnded, phoneSocket.ClosedWith);
            Assert.Null(laptopSocket.ClosedWith);
        }

        [Fact]
        public async Task SigningOutEverywhere_ClosesEverySessionsSocket_AsSessionEnded()
        {
            var phone = await SignInAsync("phone");
            var laptop = await SignInAsync("laptop");
            var phoneSocket = _a.Connect(phone.Principal);
            var laptopSocket = _b.Connect(laptop.Principal);

            await _a.UseAsync<RefreshSignInManager>(m => m.LogoutAllAsync(_alice.Id, Ct));

            Assert.Equal(CloseCodes.SessionEnded, phoneSocket.ClosedWith);
            Assert.Equal(CloseCodes.SessionEnded, laptopSocket.ClosedWith);
        }

        [Fact]
        public async Task AReplayedRefreshToken_ClosesThatSessionsSocket()
        {
            var phone = await SignInAsync("phone");
            var phoneSocket = _a.Connect(phone.Principal);
            await _a.UseAsync<RefreshSignInManager>(async m => _ = (await m.RefreshAsync(phone.RefreshToken, Ct)).Ok());
            _time.Advance(TimeSpan.FromMinutes(5));

            // The old token again, after the grace period: taken for stolen.
            var replay = await _a.UseAsync<RefreshSignInManager, bool>(
                async m => (await m.RefreshAsync(phone.RefreshToken, Ct)).Successful);

            Assert.False(replay);
            Assert.Equal(CloseCodes.SessionEnded, phoneSocket.ClosedWith);
        }

        [Fact]
        public async Task RefreshedTokens_KeepTheSessionId()
        {
            var phone = await SignInAsync("phone");

            var refreshed = await _a.UseAsync<RefreshSignInManager, TokenPair>(
                async m => (await m.RefreshAsync(phone.RefreshToken, Ct)).Ok());
            var principal = await ValidateAsync(refreshed.AccessToken);

            Assert.Equal(phone.SessionId.ToString(), principal.FindFirstValue(EchoPhaseClaimTypes.SessionId));
            Assert.Equal(phone.SessionId, _a.Gateway.Add(_alice.Id, new FakeSocket(), principal).SessionId);
        }

        [Fact]
        public async Task TheMonitor_ClosesASocketWhoseSessionEnded_WhenTheOrderWasMissed()
        {
            var phone = await SignInAsync("phone");
            var phoneSocket = _a.Connect(phone.Principal);
            // A cached "valid" verdict must not outlive the session.
            Assert.Equal(PrincipalValidity.Valid, await _a.RevalidateAsync(phone.Principal));

            // Removed without the close order (as if the backplane was down).
            await _a.UseAsync<PostgresContext>(async db =>
            {
                db.Sessions.Remove(await db.Sessions.SingleAsync(s => s.Id == phone.SessionId, Ct));
                await db.SaveChangesAsync(Ct);
            });
            Assert.Null(phoneSocket.ClosedWith);

            await _a.RevalidateAsync();

            Assert.Equal(CloseCodes.SessionEnded, phoneSocket.ClosedWith);
        }

        [Fact]
        public async Task ATokenWithoutSessionId_KeepsTodaysBehaviour()
        {
            var phone = await SignInAsync("phone");
            // A token issued before the claim existed (also what API clients and impersonations get).
            var legacy = await ValidateAsync(await _a.UseAsync<IJwtTokenProvider, string>(
                jwt => jwt.GenerateTokenAsync(_alice, TimeSpan.FromHours(2), new ClaimsEnrichmentContext { User = _alice })));
            var legacySocket = _a.Connect(legacy);
            Assert.Null(_a.Gateway.GetConnections(_alice.Id).Single(c => c.User == legacy).SessionId);

            // Another session ends: the token without a session is not touched.
            await _a.UseAsync<RefreshSignInManager>(m => m.LogoutAsync(_alice.Id, phone.RefreshToken, Ct));
            await _a.RevalidateAsync();
            Assert.Null(legacySocket.ClosedWith);

            // A security stamp change still revokes it, with the old code.
            await _a.UseAsync<RefreshSignInManager>(m => m.LogoutAllAsync(_alice.Id, Ct));
            Assert.Null(legacySocket.ClosedWith);
            await _a.RevalidateAsync();
            Assert.Equal(WebSocketCloseStatus.PolicyViolation, legacySocket.ClosedWith);
        }

        private sealed record SignedIn(Guid SessionId, string RefreshToken, ClaimsPrincipal Principal);

        private async Task<SignedIn> SignInAsync(string device)
        {
            var initial = await _a.UseAsync<ISessionProvider, TokenInitial>(s => s.CreateAsync(_alice, device, Ct));
            var principal = await ValidateAsync(initial.Tokens.AccessToken);
            return new SignedIn(initial.SessionId, Must.Present(initial.Tokens.RefreshToken), principal);
        }

        private async Task<ClaimsPrincipal> ValidateAsync(string token)
            => Must.Present(await _a.UseAsync<IJwtTokenProvider, ClaimsPrincipal?>(jwt => jwt.ValidateTokenAsync(token, Ct)));

        private ServiceProvider Build(string replica)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<TimeProvider>(_time);
            services.AddSingleton<IHostApplicationLifetime, Lifetime>();
            services.AddSingleton(Options.Create(new AuthenticationOptions
            {
                Bearer = new BearerOptions
                {
                    Issuer = "test",
                    Audience = "test",
                    ValidIssuer = "test",
                    ValidAudiences = ["test"]
                }
            }));
            services.AddSingleton(Options.Create(new GatewayOptions()));
            services.AddSingleton(Options.Create(new Argon2Options()));
            services.AddSingleton<IKeyVault, FixedKeyVault>();
            services.AddSingleton<KeyRing>();
            services.AddSingleton<IPrincipalVerdictCache>(_verdicts);
            services.AddSingleton<PrincipalVerdictInvalidationInterceptor>();
            services.AddDbContext<PostgresContext>((sp, options) => options
                .UseInMemoryDatabase(_databaseName, _database)
                .AddInterceptors(sp.GetRequiredService<PrincipalVerdictInvalidationInterceptor>()));
            services.AddScoped<UserRepository>();
            services.AddScoped<SessionRepository>();
            services.AddScoped<ApiClientRepository>();
            services.AddScoped<ImpersonationRepository>();
            services.AddIdentityCore<User>()
                .AddRoles<UserRole>()
                .AddEntityFrameworkStores<PostgresContext>();
            services.AddSingleton<PasswordHashingGate>();
            services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
            services.AddSingleton<ResourcePermissionsBitMask>();
            services.AddSingleton<IntentsBitMask>();
            services.AddSingleton<SessionTokenHasher>();
            services.AddScoped<IUserPrincipalFactory, UserPrincipalFactory>();
            services.AddScoped<IJwtTokenProvider, JwtTokenProvider>();
            services.AddScoped<ISessionProvider, SessionProvider>();
            services.AddScoped<ISessionEvents, SessionEventsNotifier>();
            services.AddScoped<RefreshSignInManager>();

            services.AddSingleton<WebSocketConnectionManager>();
            services.AddSingleton<IGatewayBackplane>(new BusBackplane(_bus, replica));
            services.AddSingleton<GatewayDispatcher>();
            services.AddSingleton<RealtimeCredentialMonitor>();

            var provider = services.BuildServiceProvider();
            var dispatcher = provider.GetRequiredService<GatewayDispatcher>();
            provider.GetRequiredService<IGatewayBackplane>()
                .SubscribeAsync(dispatcher.DeliverAsync, dispatcher.CloseAsync, CancellationToken.None)
                .GetAwaiter().GetResult();
            return provider;
        }

        /// <summary>One server replica: its own gateway connections, the shared database and backplane.</summary>
        private sealed class Replica
        {
            public Replica(ServiceProvider services)
            {
                Services = services;
            }

            public ServiceProvider Services { get; }

            public WebSocketConnectionManager Gateway => Services.GetRequiredService<WebSocketConnectionManager>();

            public FakeSocket Connect(ClaimsPrincipal principal)
            {
                var socket = new FakeSocket();
                var userId = Guid.Parse(Must.Present(principal.FindFirstValue("sub")));
                Gateway.Add(userId, socket, principal);
                return socket;
            }

            public Task RevalidateAsync()
                => Services.GetRequiredService<RealtimeCredentialMonitor>().RevalidateAsync(TestContext.Current.CancellationToken);

            public Task<PrincipalValidity> RevalidateAsync(ClaimsPrincipal principal)
                => UseAsync<IJwtTokenProvider, PrincipalValidity>(
                    jwt => jwt.RevalidateAsync(principal, TestContext.Current.CancellationToken));

            public async Task UseAsync<TService>(Func<TService, Task> use) where TService : notnull
            {
                await using var scope = Services.CreateAsyncScope();
                await use(scope.ServiceProvider.GetRequiredService<TService>());
            }

            public async Task<TResult> UseAsync<TService, TResult>(Func<TService, Task<TResult>> use) where TService : notnull
            {
                await using var scope = Services.CreateAsyncScope();
                return await use(scope.ServiceProvider.GetRequiredService<TService>());
            }
        }

        /// <summary>The replicas' pub/sub: what one publishes, all of them get.</summary>
        private sealed class Bus
        {
            public List<BusBackplane> Replicas { get; } = [];
        }

        private sealed class BusBackplane : IGatewayBackplane
        {
            private readonly Bus _bus;
            private Func<GatewayDelivery, Task>? _deliver;
            private Func<GatewayClose, Task>? _close;

            public BusBackplane(Bus bus, string replicaId)
            {
                _bus = bus;
                ReplicaId = replicaId;
                bus.Replicas.Add(this);
            }

            public string ReplicaId { get; }

            public Task PublishAsync(GatewayDelivery delivery, CancellationToken ct = default)
                => Task.WhenAll(_bus.Replicas.Select(r => r._deliver is { } deliver ? deliver(delivery) : Task.CompletedTask));

            public Task PublishCloseAsync(GatewayClose close, CancellationToken ct = default)
                => Task.WhenAll(_bus.Replicas.Select(r => r._close is { } handle ? handle(close) : Task.CompletedTask));

            public Task SubscribeAsync(Func<GatewayDelivery, Task> deliver, Func<GatewayClose, Task> close, CancellationToken ct = default)
            {
                _deliver = deliver;
                _close = close;
                return Task.CompletedTask;
            }

            public Task JoinAsync(WebSocketConnection connection, CancellationToken ct = default) => Task.CompletedTask;
            public Task LeaveAsync(WebSocketConnection connection, CancellationToken ct = default) => Task.CompletedTask;
            public Task ForgetAsync(PoolMember member, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<PoolMember>> MembersAsync(IReadOnlyCollection<Guid>? userIds, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<PoolMember>>([]);
            public Task HeartbeatAsync(CancellationToken ct = default) => Task.CompletedTask;
        }

        /// <summary>The shared verdict cache (Redis in production), in memory.</summary>
        private sealed class MemoryVerdicts : IPrincipalVerdictCache
        {
            private readonly ConcurrentDictionary<string, Guid?> _valid = new();

            public Task<bool> IsKnownValidAsync(ClaimsPrincipal principal)
                => Task.FromResult(_valid.ContainsKey(PrincipalVerdictKey.For(principal)));

            public Task RememberValidAsync(ClaimsPrincipal principal)
            {
                _valid[PrincipalVerdictKey.For(principal)] = PrincipalVerdictKey.UserId(principal);
                return Task.CompletedTask;
            }

            public Task InvalidateAsync(IEnumerable<Guid> userIds)
            {
                var users = userIds.ToHashSet();
                foreach (var entry in _valid)
                    if (entry.Value is { } user && users.Contains(user))
                        _valid.TryRemove(entry.Key, out _);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeSocket : WebSocket
        {
            private WebSocketState _state = WebSocketState.Open;

            /// <summary>The status the server closed with; null while open.</summary>
            public WebSocketCloseStatus? ClosedWith { get; private set; }

            public override WebSocketCloseStatus? CloseStatus => ClosedWith;
            public override string? CloseStatusDescription => null;
            public override WebSocketState State => _state;
            public override string? SubProtocol => null;

            public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
                CancellationToken cancellationToken) => Task.CompletedTask;

            public override void Abort() => _state = WebSocketState.Aborted;
            public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
                => CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
            public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            {
                ClosedWith = closeStatus;
                _state = WebSocketState.CloseSent;
                return Task.CompletedTask;
            }
            public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
                => throw new NotSupportedException();
            public override void Dispose() { }
        }

        private sealed class Lifetime : IHostApplicationLifetime
        {
            public CancellationToken ApplicationStarted => CancellationToken.None;
            public CancellationToken ApplicationStopping => CancellationToken.None;
            public CancellationToken ApplicationStopped => CancellationToken.None;
            public void StopApplication() { }
        }
    }
}
