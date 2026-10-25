// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using EchoPhase.Security.BitMasks;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Types.BitMask;
using EchoPhase.WebSockets;
using EchoPhase.WebSockets.Cluster;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GatewayOptions = EchoPhase.Configuration.WebSocket.WebSocketOptions;

namespace EchoPhase.Server.Tests
{
    /// <summary>Which member of a pool gets a key, and what moves when members come and go.</summary>
    public class PoolRoutingTests
    {
        private static PoolMember Member(int i) => new(Guid.Empty, "bots", "r1", new Guid(i, 0, 0, new byte[8]), BitMaskBase.Empty);

        private static readonly string[] Keys = Enumerable.Range(0, 3000).Select(i => $"run-{i}").ToArray();

        [Fact]
        public void TheSameKey_GoesToTheSameMember_WhateverTheOrder()
        {
            var members = Enumerable.Range(1, 3).Select(Member).ToList();
            var reversed = Enumerable.Reverse(members).ToList();

            Assert.All(Keys, key => Assert.Equal(PoolRouting.Pick(members, key)?.Id, PoolRouting.Pick(reversed, key)?.Id));
        }

        [Fact]
        public void KeysSpreadEvenly()
        {
            var members = Enumerable.Range(1, 3).Select(Member).ToList();
            var counts = Keys.GroupBy(key => PoolRouting.Pick(members, key)?.Id).Select(g => g.Count()).ToList();

            Assert.Equal(3, counts.Count);
            Assert.All(counts, count => Assert.InRange(count, 800, 1200));
        }

        [Fact]
        public void AMemberLeaving_MovesOnlyItsOwnKeys()
        {
            var members = Enumerable.Range(1, 3).Select(Member).ToList();
            var before = Keys.ToDictionary(key => key, key => PoolRouting.Pick(members, key)?.Id);
            var leaving = members[1];

            var after = Keys.ToDictionary(key => key, key => PoolRouting.Pick(members.Where(m => m != leaving), key)?.Id);

            Assert.All(Keys.Where(key => before[key] != leaving.Id), key => Assert.Equal(before[key], after[key]));
            Assert.All(Keys, key => Assert.NotEqual(leaving.Id, after[key]));
        }

        [Fact]
        public void Excluded_IsSkipped_AndNoneLeftIsNull()
        {
            var only = Member(1);

            Assert.Null(PoolRouting.Pick([only], "k", new HashSet<string> { only.Id }));
            Assert.Null(PoolRouting.Pick([], "k"));
        }

        [Fact]
        public void IntentsSurviveTheTrip()
        {
            var bits = new BitArray(11) { [0] = true, [3] = true, [10] = true };

            var read = Must.Present(IntentBits.Read(IntentBits.Write(bits)));

            Assert.Equal(11, read.Length);
            Assert.Equal([true, false, false, true], Enumerable.Range(0, 4).Select(i => read[i]));
            Assert.True(read[10]);
            Assert.Null(IntentBits.Read("nonsense"));
        }
    }

    /// <summary>
    /// Delivery on one replica: every shared connection, one member of each
    /// shard pool, intents respected, a failed pick redelivered in its pool.
    /// </summary>
    public class GatewayDispatcherTests : IDisposable
    {
        private readonly IntentsBitMask _intents = new();
        private readonly Lifetime _lifetime = new();
        private readonly WebSocketConnectionManager _manager;
        private readonly LocalGatewayBackplane _backplane;
        private readonly GatewayDispatcher _dispatcher;
        private readonly Guid _user = Guid.NewGuid();

        public GatewayDispatcherTests()
        {
            _manager = new WebSocketConnectionManager(NullLogger<WebSocketConnectionManager>.Instance,
                Options.Create(new GatewayOptions()), _lifetime, TimeProvider.System);
            _backplane = new LocalGatewayBackplane(_manager);
            _dispatcher = new GatewayDispatcher(_backplane, _manager, _intents, NullLogger<GatewayDispatcher>.Instance);
            _backplane.SubscribeAsync(_dispatcher.DeliverAsync, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        }

        public void Dispose() => _lifetime.Dispose();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private (WebSocketConnection Connection, FakeSocket Socket) Connect(string? pool, string intent = Intents.Blocks, bool broken = false)
        {
            var socket = new FakeSocket { Broken = broken };
            var connection = _manager.Add(_user, socket, new ClaimsPrincipal());
            connection.Intents = _intents.Add(BitMaskBase.Empty, intent);
            connection.ShardPool = pool;
            return (connection, socket);
        }

        private Task SendAsync(string? key = null)
            => _dispatcher.SendAsync([_user], Encoding.UTF8.GetBytes("{\"type\":\"x\"}"), [Intents.Blocks], key, Ct);

        [Fact]
        public async Task EveryShared_AndOneOfEachPool()
        {
            var shared = Enumerable.Range(0, 2).Select(_ => Connect(null).Socket).ToList();
            var poolA = Enumerable.Range(0, 3).Select(_ => Connect("a").Socket).ToList();
            var poolB = Enumerable.Range(0, 3).Select(_ => Connect("b").Socket).ToList();

            await SendAsync();

            Assert.All(shared, s => Assert.Equal(1, s.Sent));
            Assert.Equal(1, poolA.Sum(s => s.Sent));
            Assert.Equal(1, poolB.Sum(s => s.Sent));
        }

        [Fact]
        public async Task TheSameKey_ReachesTheSameMember()
        {
            var pool = Enumerable.Range(0, 3).Select(_ => Connect("a").Socket).ToList();

            for (int i = 0; i < 5; i++)
                await SendAsync("run-42");

            Assert.Single(pool, s => s.Sent == 5);
        }

        [Fact]
        public async Task OnlyConnectionsWithTheIntent()
        {
            var sharedWithout = Connect(null, Intents.Notifications).Socket;
            var memberWithout = Connect("a", Intents.Notifications).Socket;
            var memberWith = Connect("a").Socket;

            await SendAsync();

            Assert.Equal(0, sharedWithout.Sent);
            Assert.Equal(0, memberWithout.Sent);
            Assert.Equal(1, memberWith.Sent);
        }

        [Fact]
        public async Task AFailedPick_GoesToAnotherMember_AndSharedOnesGetItOnce()
        {
            var shared = Connect(null).Socket;
            var broken = Connect("a", broken: true);
            var healthy = Connect("a").Socket;
            // A key the broken member wins.
            var members = await _backplane.MembersAsync([_user], Ct);
            var key = Enumerable.Range(0, 1000).Select(i => $"k{i}")
                .First(k => PoolRouting.Pick(members, k)?.ConnectionId == broken.Connection.Id);

            await SendAsync(key);

            Assert.Equal(1, broken.Socket.Attempts);
            Assert.Equal(1, healthy.Sent);
            Assert.Equal(1, shared.Sent);
        }

        [Fact]
        public async Task APoolWithNobodyAlive_IsGivenUp()
        {
            var a = Connect("a", broken: true);
            var b = Connect("a", broken: true);

            await SendAsync();

            // Each tried once, then nobody is left.
            Assert.Equal(1, a.Socket.Attempts);
            Assert.Equal(1, b.Socket.Attempts);
        }

        private sealed class FakeSocket : WebSocket
        {
            public bool Broken { get; set; }
            public int Sent { get; private set; }
            public int Attempts { get; private set; }
            private WebSocketState _state = WebSocketState.Open;

            public override WebSocketCloseStatus? CloseStatus => null;
            public override string? CloseStatusDescription => null;
            public override WebSocketState State => _state;
            public override string? SubProtocol => null;

            public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
                CancellationToken cancellationToken)
            {
                Attempts++;
                if (Broken)
                {
                    // As a dropped peer: the send fails and the socket is gone.
                    _state = WebSocketState.Aborted;
                    throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
                }
                Sent++;
                return Task.CompletedTask;
            }

            public override void Abort() => _state = WebSocketState.Aborted;
            public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
                => Task.CompletedTask;
            public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
                => Task.CompletedTask;
            public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
                => throw new NotSupportedException();
            public override void Dispose() { }
        }

        private sealed class Lifetime : IHostApplicationLifetime, IDisposable
        {
            private readonly CancellationTokenSource _stopping = new();
            public CancellationToken ApplicationStarted => CancellationToken.None;
            public CancellationToken ApplicationStopping => _stopping.Token;
            public CancellationToken ApplicationStopped => CancellationToken.None;
            public void StopApplication() { }
            public void Dispose() => _stopping.Dispose();
        }
    }
}
