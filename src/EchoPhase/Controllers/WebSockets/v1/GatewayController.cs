// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;
using EchoPhase.Identity;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.WebSockets;
using EchoPhase.WebSockets.Processors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using GatewayOptions = EchoPhase.Configuration.WebSocket.WebSocketOptions;

namespace EchoPhase.Controllers.WebSockets.v1
{
    /// <summary>
    /// The realtime gateway: a WebSocket per client, messages { op, d }.
    /// One receive loop per socket; sends come from anywhere through the
    /// connection (serialized there).
    /// </summary>
    [Route("/gateway")]
    [ApiController]
    public class GatewayController : ControllerBase
    {
        private const int ReceiveChunk = 4 * 1024;

        private readonly IUserService _userService;
        private readonly WebSocketConnectionManager _connections;
        private readonly WebSocketProcessor _processor;
        private readonly GatewayOptions _settings;
        private readonly ILogger<GatewayController> _logger;

        public GatewayController(
            IUserService userService,
            WebSocketConnectionManager connections,
            WebSocketProcessor processor,
            IOptions<GatewayOptions> settings,
            ILogger<GatewayController> logger)
        {
            _userService = userService;
            _connections = connections;
            _processor = processor;
            _settings = settings.Value;
            _logger = logger;
        }

        [HttpGet]
        [Route("", Name = "WSGateway")]
        [RequirePermission(Resources.Gateway, Permissions.Connect)]
        public async Task<IActionResult> Gateway()
        {
            if (!HttpContext.WebSockets.IsWebSocketRequest)
                return BadRequest("Invalid WebSocket request.");

            // Cookies authenticate the socket, so a page on another site must
            // not open one in the user's name (cross-site WebSocket hijacking).
            if (!IsAllowedOrigin(Request.Headers.Origin))
                return StatusCode(StatusCodes.Status403Forbidden, "Origin not allowed.");

            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

            // Fires when the client goes away or the server shuts down.
            var ct = HttpContext.RequestAborted;
            var connection = _connections.Add(user.Id, webSocket, User);
            try
            {
                await ReceiveLoopAsync(connection, ct);
            }
            finally
            {
                _connections.Remove(connection);
            }

            return new EmptyResult();
        }

        // No Origin: not a browser. Otherwise the same host as the request
        // (what the webapp's nginx forwards), or one configured as allowed.
        private bool IsAllowedOrigin(string? origin)
        {
            if (string.IsNullOrEmpty(origin))
                return true;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                return false;

            var authority = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            return string.Equals(authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase)
                || _settings.AllowedOrigins.Contains(origin.TrimEnd('/'), StringComparer.OrdinalIgnoreCase);
        }

        private async Task ReceiveLoopAsync(WebSocketConnection connection, CancellationToken ct)
        {
            var socket = connection.WebSocket;
            var chunk = new byte[ReceiveChunk];
            using var message = new MemoryStream();

            try
            {
                while (socket.State is WebSocketState.Open or WebSocketState.CloseSent)
                {
                    // A message may span frames: collect until its end, within the limit.
                    message.SetLength(0);
                    ValueWebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(chunk.AsMemory(), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            _logger.LogInformation("WebSocket closed by client. UserId: {UserId}, Status: {Status}",
                                connection.UserId, socket.CloseStatus);
                            await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", _settings.CloseTimeout);
                            return;
                        }

                        if (message.Length + result.Count > _settings.MaxMessageBytes)
                        {
                            await connection.CloseAsync(WebSocketCloseStatus.MessageTooBig,
                                $"Messages are limited to {_settings.MaxMessageBytes} bytes", _settings.CloseTimeout);
                            return;
                        }
                        message.Write(chunk, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    _connections.Touch(connection);

                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        await connection.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Text messages only", _settings.CloseTimeout);
                        return;
                    }

                    try
                    {
                        await _processor.HandleMessageAsync(connection, message.GetBuffer().AsMemory(0, (int)message.Length), ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException and not WebSocketException)
                    {
                        // One bad message is that message's problem, not the connection's.
                        _logger.LogError(ex, "Error handling a gateway message. UserId: {UserId}", connection.UserId);
                    }
                }
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.LogInformation("WebSocket connection dropped. UserId: {UserId}", connection.UserId);
            }
            catch (OperationCanceledException)
            {
                // Client gone or server stopping.
            }
        }
    }
}
