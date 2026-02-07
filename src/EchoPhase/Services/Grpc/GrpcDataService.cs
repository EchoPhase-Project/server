using EchoPhase.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace EchoPhase.Services.Grpc
{
    [Authorize]
    public class GrpcDataService : TransferData.TransferDataBase
    {
        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new HelloReply
            {
                Message = $"Привет, {request.Name}"
            });
        }
    }
}
