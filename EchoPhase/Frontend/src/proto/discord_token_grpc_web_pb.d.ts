import * as grpcWeb from 'grpc-web';

import * as discord_token_pb from './discord_token_pb'; // proto import: "discord_token.proto"


export class DiscordTokenGrpcServiceClient {
  constructor (hostname: string,
               credentials?: null | { [index: string]: string; },
               options?: null | { [index: string]: any; });

  get(
    request: discord_token_pb.DiscordTokenSearchOptionsGrpc,
    metadata: grpcWeb.Metadata | undefined,
    callback: (err: grpcWeb.RpcError,
               response: discord_token_pb.DiscordTokenListGrpc) => void
  ): grpcWeb.ClientReadableStream<discord_token_pb.DiscordTokenListGrpc>;

  create(
    request: discord_token_pb.DiscordTokenGrpc,
    metadata: grpcWeb.Metadata | undefined,
    callback: (err: grpcWeb.RpcError,
               response: discord_token_pb.DiscordTokenGrpc) => void
  ): grpcWeb.ClientReadableStream<discord_token_pb.DiscordTokenGrpc>;

  createBatch(
    request: discord_token_pb.DiscordTokenListGrpc,
    metadata: grpcWeb.Metadata | undefined,
    callback: (err: grpcWeb.RpcError,
               response: discord_token_pb.DiscordTokenResultGrpc) => void
  ): grpcWeb.ClientReadableStream<discord_token_pb.DiscordTokenResultGrpc>;

  edit(
    request: discord_token_pb.EditRequestGrpc,
    metadata: grpcWeb.Metadata | undefined,
    callback: (err: grpcWeb.RpcError,
               response: discord_token_pb.DiscordTokenGrpc) => void
  ): grpcWeb.ClientReadableStream<discord_token_pb.DiscordTokenGrpc>;

  editBatch(
    request: discord_token_pb.EditBatchRequestGrpc,
    metadata: grpcWeb.Metadata | undefined,
    callback: (err: grpcWeb.RpcError,
               response: discord_token_pb.DiscordTokenResultGrpc) => void
  ): grpcWeb.ClientReadableStream<discord_token_pb.DiscordTokenResultGrpc>;

  delete(
    request: discord_token_pb.DiscordTokenGrpc,
    metadata: grpcWeb.Metadata | undefined,
    callback: (err: grpcWeb.RpcError,
               response: discord_token_pb.DiscordTokenGrpc) => void
  ): grpcWeb.ClientReadableStream<discord_token_pb.DiscordTokenGrpc>;

  deleteBatch(
    request: discord_token_pb.DiscordTokenListGrpc,
    metadata: grpcWeb.Metadata | undefined,
    callback: (err: grpcWeb.RpcError,
               response: discord_token_pb.DiscordTokenResultGrpc) => void
  ): grpcWeb.ClientReadableStream<discord_token_pb.DiscordTokenResultGrpc>;

}

export class DiscordTokenGrpcServicePromiseClient {
  constructor (hostname: string,
               credentials?: null | { [index: string]: string; },
               options?: null | { [index: string]: any; });

  get(
    request: discord_token_pb.DiscordTokenSearchOptionsGrpc,
    metadata?: grpcWeb.Metadata
  ): Promise<discord_token_pb.DiscordTokenListGrpc>;

  create(
    request: discord_token_pb.DiscordTokenGrpc,
    metadata?: grpcWeb.Metadata
  ): Promise<discord_token_pb.DiscordTokenGrpc>;

  createBatch(
    request: discord_token_pb.DiscordTokenListGrpc,
    metadata?: grpcWeb.Metadata
  ): Promise<discord_token_pb.DiscordTokenResultGrpc>;

  edit(
    request: discord_token_pb.EditRequestGrpc,
    metadata?: grpcWeb.Metadata
  ): Promise<discord_token_pb.DiscordTokenGrpc>;

  editBatch(
    request: discord_token_pb.EditBatchRequestGrpc,
    metadata?: grpcWeb.Metadata
  ): Promise<discord_token_pb.DiscordTokenResultGrpc>;

  delete(
    request: discord_token_pb.DiscordTokenGrpc,
    metadata?: grpcWeb.Metadata
  ): Promise<discord_token_pb.DiscordTokenGrpc>;

  deleteBatch(
    request: discord_token_pb.DiscordTokenListGrpc,
    metadata?: grpcWeb.Metadata
  ): Promise<discord_token_pb.DiscordTokenResultGrpc>;

}

