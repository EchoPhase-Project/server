using Microsoft.AspNetCore.Authorization;

using Grpc.Core;

using EchoPhase.Models;
using EchoPhase.Services;
using EchoPhase.Interfaces;

namespace Shared.Grpc
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
