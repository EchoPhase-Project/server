using System.Net.WebSockets;

using EchoPhase.Attributes;

namespace EchoPhase.Services.WebSockets.Models
{
	public class WebSocketConnection
	{
		public WebSocket WebSocket { get; set; } = default!;
		public HttpContext HttpContext { get; set; } = default!;

		[AlwaysMerge]
		public long Intents { get; set; } = 0;

		public CancellationTokenSource HeartbeatCancellationTokenSource { get; set; } = new();
	}
}
