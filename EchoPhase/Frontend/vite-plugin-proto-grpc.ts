// vite-plugin-proto-grpc.ts
import { Plugin } from 'vite';
import { execSync } from 'child_process';
import path from 'path';

export default function protoGRPCPlugin(): Plugin {
  return {
    name: 'proto-grpc-generator',
    buildStart() {
      const protoDir = path.resolve(__dirname, '../Protos');
      const outDir = path.resolve(__dirname, './src/proto');
      const protoFile = 'discord_token.proto';

      console.log('[proto-grpc] Generating protobuf/grpc files...');
      execSync(`protoc -I=${protoDir} ${protoFile} \
        --js_out=import_style=commonjs:${outDir} \
        --grpc-web_out=import_style=typescript,mode=grpcwebtext:${outDir}`, {
        stdio: 'inherit',
      });
      console.log('[proto-grpc] Done!');
    },
  };
}
