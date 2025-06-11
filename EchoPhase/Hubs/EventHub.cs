using EchoPhase.Interfaces;
using EchoPhase.Models;
using EchoPhase.Services.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EchoPhase.Hubs
{
    [Route("eventHub", Name = "EventHub")]
    [Authorize]
    public class EventHub : Hub
    {
        private readonly IUserConnectionManager _userConnectionManager;
        private readonly IUserService _userService;

        public EventHub(
                IUserConnectionManager userConnectionManager,
                IUserService userService)
        {
            _userConnectionManager = userConnectionManager;
            _userService = userService;
        }

        public async Task Echo(string message)
        {
            if (Context.User is null)
            {
                Console.WriteLine("Invalid attemt.");
                Context.Abort();
                return;
            }

            User? sender = await _userService.GetAsync(Context.User);
            if (sender is null)
            {
                Console.WriteLine("Can't find user.");
                Context.Abort();
                return;
            }

            var recipientConnectionId = _userConnectionManager.GetConnectionId(sender.Id);

            if (recipientConnectionId is null)
            {
                Console.WriteLine("Can't find connection id.");
                Context.Abort();
                return;
            }

            await Clients.Client(recipientConnectionId).SendAsync("Echo", message);
        }

        public override async Task OnConnectedAsync()
        {
            if (ShutdownService.IsShuttingDown)
            {
                await Clients.Caller.SendAsync("ShutdownNotification", "Server is shutting down. Disconnecting...");
                Console.WriteLine("Connection attempt rejected due to server shutdown.");
                Context.Abort();
                return;
            }

            if (Context.User is null)
            {
                Console.WriteLine("Invalid attemt.");
                Context.Abort();
                return;
            }

            User? user = await _userService.GetAsync(Context.User);
            if (user is null)
            {
                Console.WriteLine("Can't find user.");
                Context.Abort();
                return;
            }

            _userConnectionManager.KeepUserConnection(user.Id, Context.ConnectionId);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (Context.User == null)
                return;

            User? user = await _userService.GetAsync(Context.User);
            if (user is null)
            {
                Console.WriteLine("Can't find user.");
                Context.Abort();
                return;
            }

            _userConnectionManager.RemoveUserConnection(user.Id, Context.ConnectionId);

            await base.OnDisconnectedAsync(exception);
        }
    }
}
