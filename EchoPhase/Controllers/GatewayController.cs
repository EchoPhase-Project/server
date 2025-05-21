using System.Text;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using ParkSquare.AspNetCore.Sitemap;

using EchoPhase.DAL.Postgres;
using EchoPhase.Services.WebSockets;
using EchoPhase.Processors;
using EchoPhase.Interfaces;

namespace EchoPhase.Controllers
{
	[Route("/gateway")]
	[ApiController]
	[SitemapExclude]
	public class GatewayController : ControllerBase
	{
        private readonly PostgresContext _context;
		private readonly IUserService _userService;
		private readonly WebSocketConnectionManager _connectionManager;
		private readonly WebSocketProcessor _webSocketProcessor;
		private readonly ILogger<GatewayController> _logger;
		private readonly IWebHostEnvironment _env;

		public GatewayController(PostgresContext context,
				IUserService userService,
				WebSocketConnectionManager connectionManager,
				WebSocketProcessor webSocketProcessor,
				ILogger<GatewayController> logger,
				IWebHostEnvironment env)
		{
            _context = context;
			_userService = userService;
			_connectionManager = connectionManager;
			_webSocketProcessor = webSocketProcessor;
			_logger = logger;
			_env = env;
		}

        [HttpGet]
		[Route("", Name = "WSGateway")]
		[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "AdminOrDevOnly")]
		public async Task<IActionResult> Gateway()
		{
			if (HttpContext.WebSockets.IsWebSocketRequest)
			{
					var user = await _userService.GetUserAsync(User);

					if (user is null)
						return Unauthorized();

					using (WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync())
					{
						_connectionManager.AddConnection(user.Id, webSocket, HttpContext);
						
						await HandleWebSocketAsync(HttpContext, webSocket, user.Id);
					}

					return new EmptyResult();
			}
			else
			{
				return BadRequest("Invalid WebSocket request.");
			}
		}

		private async Task HandleWebSocketAsync(HttpContext context, WebSocket webSocket, Guid userId)
		{
			var buffer = new byte[1024 * 4];
			WebSocketReceiveResult result;

			try
			{
				do
				{
					result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

					if (result.MessageType == WebSocketMessageType.Text)
					{
						var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
						
						await _webSocketProcessor.HandleMessageAsync(webSocket, message);
						
						if (_env.IsDevelopment())
						{
							_logger.LogInformation($"Received message: {message} <{Thread.CurrentThread.ManagedThreadId}>");
						}
					}
					else if (result.MessageType == WebSocketMessageType.Close)
					{
						await _connectionManager.CloseConnectionAsync(userId, webSocket);
					}
				}
				while (!result.CloseStatus.HasValue && webSocket.State == WebSocketState.Open);
			}
			catch (WebSocketException ex)
			{
				_logger.LogError($"WebSocket exception: {ex.Message} <{Thread.CurrentThread.ManagedThreadId}>");
			}
			catch (Exception ex)
			{
				_logger.LogError($"Exception: {ex.Message} <{Thread.CurrentThread.ManagedThreadId}>");
			}
			finally
			{
				if (webSocket.State == WebSocketState.Open)
				{
					await _connectionManager.CloseConnectionAsync(userId, webSocket);
				}
			}
		}
	}
}
