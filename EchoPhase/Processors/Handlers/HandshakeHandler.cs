using System.Net.WebSockets;
using EchoPhase.Attributes;
using EchoPhase.Exceptions;
using EchoPhase.Extensions;
using EchoPhase.Interfaces;
using EchoPhase.Processors.Enums;
using EchoPhase.Processors.Payloads;
using EchoPhase.Services.Bitmasks;
using EchoPhase.Services.WebSockets;

namespace EchoPhase.Processors.Handlers
{
    [OpCodeHandler(OpCodes.Handshake)]
    public class HandshakeHandler : OpCodeHandlerBase<HandshakePayload>
    {
        private readonly WebSocketService _webSocketService;
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly IIntentsBitMaskService _intentsService;

        public HandshakeHandler(IServiceProvider serviceProvider)
        : base(serviceProvider)
        {
            _webSocketService = GetService<WebSocketService>();
            _connectionManager = GetService<WebSocketConnectionManager>();
            _intentsService = GetService<IIntentsBitMaskService>();
        }

        public override async Task HandleAsync(WebSocket webSocket, HandshakePayload payload)
        {
            try
            {
                var connection = await _connectionManager.GetConnectionAsync(webSocket);

                if (payload.Intents is not null)
                {
                    var result = IntentsBitMaskService.Deserialize(payload.Intents);

                    if (result.TryGetError(out var error))
                    {
                        var responseError = EventMessage<ErrorPayload>.Create(OpCodes.Error, err =>
                        {
                            err.Code = ErrorCodes.DeserializationError;
                            err.Message = error.Value;
                        });

                        await _webSocketService.SendMessageToConnectionAsync(webSocket, responseError);
                        return;
                    }

                    if (result.TryGetValue(out var value))
                    {
                        connection.Intents = value;
                    }
                }

                var intents = Array.Empty<string>();

                var decodingResult = _intentsService.Decode(connection.Intents);

                if (decodingResult.TryGetValue(out var decoded))
                    intents = decoded;

                var response = EventMessage<HandshakeAckPayload>.Create(OpCodes.HandshakeAck, p =>
                {
                    p.HeartbeatInterval = WebSocketConnectionManager.heartbeatInterval.TotalMilliseconds;
                    p.Intents = intents;
                });

                await _webSocketService.SendMessageToConnectionAsync(webSocket, response);
            }
            catch (WebSocketConnectionNotFoundException)
            {
                var response = EventMessage<ErrorPayload>.Create(OpCodes.Error, p =>
                {
                    p.Code = ErrorCodes.NotFound;
                    p.Message = "Connection not found.";
                });

                await _webSocketService.SendMessageToConnectionAsync(webSocket, response);

                throw;
            }

            return;
        }
    }
}
