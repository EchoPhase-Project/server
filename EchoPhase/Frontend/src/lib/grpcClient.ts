import { DiscordTokenGrpcServiceClient } from '../proto/discord_token_grpc_web_pb';

const client = new DiscordTokenGrpcServiceClient('https://localhost:5001', null, null);

export default client;
