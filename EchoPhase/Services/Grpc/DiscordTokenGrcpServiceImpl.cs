using System.Threading.Tasks;
using Grpc.Core;
using EchoPhase.Grpc;
using EchoPhase.Interfaces;
using EchoPhase.Models;
using EchoPhase.Repositories.Options;
using System.Linq;
using System.Collections.Generic;

namespace EchoPhase.Services.Grpc
{
    public class DiscordTokenGrpcServiceImpl : DiscordTokenGrpcService.DiscordTokenGrpcServiceBase
    {
        private readonly IDiscordTokenService _service;

        public DiscordTokenGrpcServiceImpl(IDiscordTokenService service)
        {
            _service = service;
        }

        // Конвертация из доменной модели в protobuf
        private static DiscordTokenGrpc ToProto(DiscordToken token) => new()
        {
            Id = token.Id.ToString(),
            UserId = token.UserId.ToString(),
            Name = token.Name ?? "",
            Token = token.Token ?? "",
            UpdatedAt = token.UpdatedAt.ToString("o"),
            CreatedAt = token.CreatedAt.ToString("o")
        };

        // Конвертация из protobuf в доменную модель
        private static DiscordToken FromProto(DiscordTokenGrpc proto) => new()
        {
            Id = Guid.TryParse(proto.Id, out var id) ? id : Guid.Empty,
            UserId = Guid.TryParse(proto.UserId, out var uid) ? uid : Guid.Empty,
            Name = proto.Name,
            Token = proto.Token,
            UpdatedAt = DateTime.TryParse(proto.UpdatedAt, out var updated) ? updated : DateTime.MinValue,
            CreatedAt = DateTime.TryParse(proto.CreatedAt, out var created) ? created : DateTime.MinValue
        };

        // Get: возвращаем список по фильтрам
        public override Task<DiscordTokenListGrpc> Get(DiscordTokenSearchOptionsGrpc request, ServerCallContext context)
        {
            var opts = new DiscordTokenSearchOptions
            {
                Ids = request.Ids.Select(id => Guid.Parse(id)).ToHashSet(),
                UserIds = request.UserIds.Select(id => Guid.Parse(id)).ToHashSet(),
                Names = request.Names.ToHashSet(),
                Tokens = request.Tokens.ToHashSet()
            };

            var tokens = _service.Get(opts);

            var reply = new DiscordTokenListGrpc();
            reply.Items.AddRange(tokens.Select(ToProto));

            return Task.FromResult(reply);
        }

        // Create один
        public override async Task<DiscordTokenGrpc> Create(DiscordTokenGrpc request, ServerCallContext context)
        {
            var token = FromProto(request);
            var created = await _service.CreateAsync(token);
            return ToProto(created);
        }

        // CreateBatch
        public override async Task<DiscordTokenResultGrpc> CreateBatch(DiscordTokenListGrpc request, ServerCallContext context)
        {
            var tokens = request.Items.Select(FromProto).ToList();
            var result = await _service.CreateAsync(tokens);

            var reply = new DiscordTokenResultGrpc();
            reply.Items.AddRange(result.Affected.Select(ToProto));
            reply.Errors.AddRange(result.Errors);
            reply.IsSucceeded = result.IsSucceeded;

            return reply;
        }

        // Edit один
        public override async Task<DiscordTokenGrpc> Edit(EditRequestGrpc request, ServerCallContext context)
        {
            var target = FromProto(request.Target);
            var modify = FromProto(request.Modify);

            // Пример: overrideFields не мапим в Expression, можно реализовать отдельно
            var edited = await _service.EditAsync(target, modify);

            return ToProto(edited);
        }

        // EditBatch
		public override async Task<DiscordTokenResultGrpc> EditBatch(EditBatchRequestGrpc request, ServerCallContext context)
		{
			var targets = request.Targets.Select(FromProto);
			var modify = FromProto(request.Modify);

			var result = await _service.EditAsync(targets, modify);

			var reply = new DiscordTokenResultGrpc();
			reply.Items.AddRange(result.Affected.Select(ToProto));
			reply.Errors.AddRange(result.Errors);
			reply.IsSucceeded = result.IsSucceeded;

			return reply;
		}

        // Delete один
        public override async Task<DiscordTokenGrpc> Delete(DiscordTokenGrpc request, ServerCallContext context)
        {
            var token = FromProto(request);
            var deleted = await _service.DeleteAsync(token);
            return ToProto(deleted);
        }

        // DeleteBatch
        public override async Task<DiscordTokenResultGrpc> DeleteBatch(DiscordTokenListGrpc request, ServerCallContext context)
        {
            var tokens = request.Items.Select(FromProto);
            var result = await _service.DeleteAsync(tokens);

            var reply = new DiscordTokenResultGrpc();
            reply.Items.AddRange(result.Affected.Select(ToProto));
            reply.Errors.AddRange(result.Errors);
            reply.IsSucceeded = result.IsSucceeded;

            return reply;
        }
    }
}
