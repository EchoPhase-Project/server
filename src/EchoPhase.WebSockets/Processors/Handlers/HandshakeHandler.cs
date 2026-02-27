using System.Net.WebSockets;
using EchoPhase.Configuration.Settings;
using EchoPhase.Security.BitMasks;
using EchoPhase.Types.Result.Extensions;
using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;
using EchoPhase.WebSockets.Exceptions;
using EchoPhase.WebSockets.Processors.Payloads;
using Microsoft.Extensions.Options;

namespace EchoPhase.WebSockets.Processors.Handlers
{
    [OpCodeHandler(OpCodes.Handshake)]
    public class HandshakeHandler : OpCodeHandlerBase<HandshakePayload>
    {
        private readonly WebSocketService _webSocketService;
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly IIntentsBitMask _intents;
        private readonly WebSocketSettings _settings;

        public HandshakeHandler(IServiceProvider serviceProvider)
        : base(serviceProvider)
        {
            _webSocketService = GetService<WebSocketService>();
            _connectionManager = GetService<WebSocketConnectionManager>();
            _intents = GetService<IIntentsBitMask>();
            _settings = GetService<IOptions<WebSocketSettings>>().Value;

        }

        public override async Task HandleAsync(WebSocket webSocket, HandshakePayload payload)
        {
            try
            {
                var connection = await _connectionManager.GetConnectionAsync(webSocket);

                if (payload.Intents is not null)
                {
                    var result = IntentsBitMask.Deserialize(payload.Intents);

                    if (result.TryGetError(out var error))
                    {
                        var responseError = EventMessage<ErrorPayload>.Create(OpCodes.Error, err =>
                        {
                            err.Code = ErrorCodes.DeserializationError;
                            err.Message = error.Value;
                        });

                        await _webSocketService.SendMessageAsync(webSocket, responseError);
                        return;
                    }

                    if (result.TryGetValue(out var value))
                    {
                        connection.Intents = value;
                    }
                }

                var intents = Array.Empty<string>();

                var decodingResult = _intents.Decode(connection.Intents);

                if (decodingResult.TryGetValue(out var decoded))
                    intents = decoded;

                var response = EventMessage<HandshakeAckPayload>.Create(OpCodes.HandshakeAck, p =>
                {
                    p.HeartbeatInterval = _settings.HeartbeatInterval.TotalMilliseconds;
                    p.Intents = intents;
                });

                await _webSocketService.SendMessageAsync(webSocket, response);
            }
            catch (WebSocketConnectionNotFoundException)
            {
                var response = EventMessage<ErrorPayload>.Create(OpCodes.Error, p =>
                {
                    p.Code = ErrorCodes.NotFound;
                    p.Message = "Connection not found.";
                });

                await _webSocketService.SendMessageAsync(webSocket, response);

                throw;
            }

            return;
        }
    }
}
