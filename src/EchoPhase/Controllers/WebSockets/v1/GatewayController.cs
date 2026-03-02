using System.Net.WebSockets;
using System.Text;
using EchoPhase.Identity;
using EchoPhase.WebSockets;
using EchoPhase.WebSockets.Processors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.WebSockets.v1
{
    [Route("/gateway")]
    [ApiController]
    public class GatewayController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly WebSocketProcessor _webSocketProcessor;
        private readonly ILogger<GatewayController> _logger;

        private const int BufferSize = 1024 * 16;

        public GatewayController(
            IUserService userService,
            WebSocketConnectionManager connectionManager,
            WebSocketProcessor webSocketProcessor,
            ILogger<GatewayController> logger)
        {
            _userService = userService;
            _connectionManager = connectionManager;
            _webSocketProcessor = webSocketProcessor;
            _logger = logger;
        }

        [HttpGet]
        [Route("", Name = "WSGateway")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "DevOrHigher")]
        public async Task<IActionResult> Gateway()
        {
            if (!HttpContext.WebSockets.IsWebSocketRequest)
            {
                return BadRequest("Invalid WebSocket request.");
            }

            var user = await _userService.GetAsync(User);
            if (user is null)
            {
                return Unauthorized();
            }

            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

            await _connectionManager.AddConnectionAsync(user.Id, webSocket, HttpContext);

            try
            {
                await HandleWebSocketAsync(webSocket, user.Id);
            }
            finally
            {
                await _connectionManager.CloseConnectionAsync(user.Id, webSocket);
            }

            return new EmptyResult();
        }

        private async Task HandleWebSocketAsync(WebSocket webSocket, Guid userId)
        {
            var buffer = new byte[BufferSize];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation(
                            "WebSocket close requested by client. UserId: {UserId}, Status: {Status}, Description: {Description}",
                            userId, result.CloseStatus, result.CloseStatusDescription);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        await ProcessTextMessageAsync(webSocket, buffer, result, userId);
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        _logger.LogWarning("Binary message received but not supported. UserId: {UserId}", userId);
                    }
                }
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.LogWarning("WebSocket connection closed prematurely. UserId: {UserId}", userId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WebSocket operation cancelled. UserId: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in WebSocket handler. UserId: {UserId}", userId);
            }
        }

        private async Task ProcessTextMessageAsync(
            WebSocket webSocket,
            byte[] buffer,
            WebSocketReceiveResult result,
            Guid userId)
        {
            try
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                _logger.LogDebug("Received message from UserId: {UserId}, Length: {Length}", userId, message.Length);

                await _webSocketProcessor.HandleMessageAsync(webSocket, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message. UserId: {UserId}", userId);

                if (webSocket.State == WebSocketState.Open)
                {
                    var errorMessage = Encoding.UTF8.GetBytes("{\"error\":\"Message processing failed\"}");
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(errorMessage),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
            }
        }
    }
}
