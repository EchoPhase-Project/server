// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Notifications;
using EchoPhase.DAL.Scylla.Entities;
using EchoPhase.Notifications;
using EchoPhase.Server.Controllers.Api.v1.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace EchoPhase.Server.Tests
{
    /// <summary>NotificationService on its ScyllaDB store (the in-memory engine): kept, delivered live, read, expiring.</summary>
    public class NotificationServiceTests : IDisposable
    {
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
        private readonly ScyllaTestDb _scylla;
        private readonly ScyllaNotificationStore _store;
        private readonly RecordingPublisher _publisher = new();
        private readonly NotificationsOptions _options = new() { Ttl = TimeSpan.FromDays(30), InboxSize = 3 };
        private readonly Guid _user = Guid.NewGuid();

        public NotificationServiceTests()
        {
            _scylla = new ScyllaTestDb(_time);
            _store = new ScyllaNotificationStore(_scylla.Context());
        }

        public void Dispose() => _scylla.Dispose();

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private NotificationService Service() =>
            new(_store, _publisher, Options.Create(_options), _time, NullLogger<NotificationService>.Instance);

        private Task<Notification> Send(string title = "Hi", string link = "")
            => Service().SendAsync(_user, new NotificationMessage(NotificationKinds.Workflow, title, "body", link,
                new Dictionary<string, string> { ["k"] = "v" }), Ct);

        [Fact]
        public async Task Send_KeepsItAndDeliversItLive()
        {
            var sent = await Send();

            var inbox = await Service().InboxAsync(_user, 10, Ct);
            Assert.Equal(sent.Id, Assert.Single(inbox.Items).Id);
            Assert.Equal(1, inbox.Unread);
            Assert.Equal(sent.Id, Assert.Single(_publisher.Published).Id);
            Assert.Equal("v", NotificationResponse.From(sent).Params["k"]);
        }

        [Fact]
        public async Task Send_WhenLiveDeliveryFails_StillKeepsIt()
        {
            _publisher.Fail = true;

            await Send();

            Assert.Single((await Service().InboxAsync(_user, 10, Ct)).Items);
        }

        [Theory]
        [InlineData("/notes?open=1", "/notes?open=1")]
        [InlineData("https://evil.example", "")]
        [InlineData("//evil.example", "")]
        [InlineData("/\\evil.example", "")]
        [InlineData("javascript:alert(1)", "")]
        public async Task Links_StayInsideTheApp(string link, string kept)
        {
            Assert.Equal(kept, (await Send(link: link)).Link);
        }

        [Fact]
        public async Task LongText_IsCut()
        {
            var sent = await Send(title: new string('x', 500));

            Assert.Equal(NotificationMessage.MaxTitleLength, sent.Title.Length);
            Assert.EndsWith("…", sent.Title);
        }

        [Fact]
        public async Task Inbox_IsNewestFirst_AndBounded()
        {
            for (var i = 0; i < 5; i++)
            {
                await Send(title: $"n{i}");
                _time.Advance(TimeSpan.FromSeconds(1));
            }

            var inbox = await Service().InboxAsync(_user, 10, Ct);

            Assert.Equal(["n4", "n3", "n2"], inbox.Items.Select(n => n.Title));
            Assert.Equal(3, inbox.Unread);
        }

        [Fact]
        public async Task MarkRead_ThenAll()
        {
            var first = await Send(title: "a");
            _time.Advance(TimeSpan.FromSeconds(1));
            await Send(title: "b");

            Assert.True((await Service().MarkReadAsync(_user, first.Id, Ct)).Successful);
            Assert.Equal(1, (await Service().InboxAsync(_user, 10, Ct)).Unread);

            Assert.Equal(1, await Service().MarkAllReadAsync(_user, Ct));
            Assert.Equal(0, (await Service().InboxAsync(_user, 10, Ct)).Unread);
        }

        [Fact]
        public async Task Reading_DoesNotExtendItsLife()
        {
            var sent = await Send();
            _time.Advance(TimeSpan.FromDays(29));
            await Service().MarkReadAsync(_user, sent.Id, Ct);

            _time.Advance(TimeSpan.FromDays(2));

            Assert.Empty((await Service().InboxAsync(_user, 10, Ct)).Items);
        }

        [Fact]
        public async Task OthersNotifications_AreNotFound()
        {
            var sent = await Send();

            Assert.False((await Service().MarkReadAsync(Guid.NewGuid(), sent.Id, Ct)).Successful);
            Assert.False((await Service().DeleteAsync(Guid.NewGuid(), sent.Id, Ct)).Successful);
            Assert.True((await Service().DeleteAsync(_user, sent.Id, Ct)).Successful);
            Assert.Empty((await Service().InboxAsync(_user, 10, Ct)).Items);
        }

        private sealed class RecordingPublisher : INotificationPublisher
        {
            public List<Notification> Published { get; } = [];
            public bool Fail { get; set; }

            public Task PublishAsync(Notification notification, CancellationToken ct = default)
            {
                if (Fail)
                    throw new InvalidOperationException("gateway down");
                Published.Add(notification);
                return Task.CompletedTask;
            }
        }
    }
}
