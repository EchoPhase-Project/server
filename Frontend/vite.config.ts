import { ViteDevServer, defineConfig } from 'vite';
import { IncomingMessage, ServerResponse } from 'http';
import solidPlugin from 'vite-plugin-solid';
import solid from 'vite-plugin-solid';
import path from 'path';
import { visualizer } from 'rollup-plugin-visualizer';
import compress from 'vite-plugin-compression';
import protoGRPCPlugin from './vite-plugin-proto-grpc';
import commonjs from '@rollup/plugin-commonjs';

const ClientSideRouting = {
    name: 'dynamic-router',
    configureServer(server: ViteDevServer) {
        server.middlewares.use(
            (req: IncomingMessage, res: ServerResponse, next: () => void) => {
                if (req.url?.search(/^\/@\d+/) !== -1) {
                    req.url = '/';
                }
                next();
            }
        );
    },
};

export default defineConfig({
    plugins: [
        solid(),
        solidPlugin(),
        visualizer({ filename: 'dist/bundle-analysis.html' }),
        compress({ algorithm: 'gzip' }),
        ClientSideRouting,
        commonjs({}),
    ],
    resolve: {
        alias: {
            '@': path.resolve(__dirname, 'src'),
            '@lib': path.resolve(__dirname, 'src/lib'),
            '@proto': path.resolve(__dirname, 'src/lib/grpc-clients/proto'),
        },
        extensions: ['.js', '.jsx', '.ts', '.tsx', '.scss', '.css'],
    },
    css: {
        preprocessorOptions: {
            scss: {
                additionalData: `@import "@/styles/global.scss";`,
            },
        },
    },
    server: {
        port: 5002,
        open: true,
        strictPort: true,
        hmr: true,
        origin: 'http://localhost:5001/app',
    },
    optimizeDeps: {
        include: ['google-protobuf', 'grpc-web', '@microsoft/signalr'],
    },
    root: path.resolve(__dirname),
    publicDir: path.resolve(__dirname, 'static'),
    // base: '/app',
    build: {
        target: 'esnext',
        outDir: 'dist',
        emptyOutDir: true,
        sourcemap: true,
        cssCodeSplit: true,
        minify: 'terser',
        commonjsOptions: {
          include: [/node_modules/],
        },
        rollupOptions: {
            input: {
                index: path.resolve(__dirname, 'index.html'),
                main: path.resolve(__dirname, 'src', 'index.tsx'),
            },
            output: {
                entryFileNames: 'js/[name].[hash].js',
                chunkFileNames: 'js/[name].[hash].js',
                assetFileNames: 'assets/[name].[hash].[ext]',
            },
        },
    },
});
