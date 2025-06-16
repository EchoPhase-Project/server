import { ViteDevServer, defineConfig } from 'vite';
import { IncomingMessage, ServerResponse } from 'http';
import { visualizer } from 'rollup-plugin-visualizer';
import solidPlugin from 'vite-plugin-solid';
import compress from 'vite-plugin-compression';
import commonjs from '@rollup/plugin-commonjs';
import path from 'path';
import runBeforeBuildPlugin from './plugins/vite-plugin-run-before-build';

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
        solidPlugin(),
        visualizer({ filename: 'dist/bundle-analysis.html' }),
        compress({ algorithm: 'gzip' }),
        ClientSideRouting,
        commonjs({}),
        runBeforeBuildPlugin({
            command: 'npm',
            args: ['run', 'gen:lib'],
        }),
        runBeforeBuildPlugin({
            command: 'npm',
            args: ['run', 'format'],
        }),
    ],
    resolve: {
        alias: {
            '@': path.resolve(__dirname, 'src'),
            '@lib': path.resolve(__dirname, 'src', 'lib'),
            '@forms': path.resolve(__dirname, 'src', 'forms'),
            '@styles': path.resolve(__dirname, 'src', 'styles'),
            '@static': path.resolve(__dirname, 'src', 'static'),
            '@shaders': path.resolve(__dirname, 'src', 'shaders'),
        },
        extensions: ['.js', '.jsx', '.ts', '.tsx', '.scss', '.css'],
    },
    css: {
        preprocessorOptions: {
            scss: {
                additionalData: [
                    `@use "@styles/variables.scss" as vars;`,
                    `@use "@styles/media.scss" as comp;`,
                    `@use "@styles/utils.scss" as utils;`,
                    `@use "@styles/prefixes.scss" as prefixes;`,
                ].join('\n'),
            },
        },
    },
    server: {
        port: 3000,
        open: true,
        strictPort: true,
        hmr: true,
        // origin: 'http://localhost:8080',
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
