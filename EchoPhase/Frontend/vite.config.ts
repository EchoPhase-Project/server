import { ViteDevServer, defineConfig } from 'vite';
import { IncomingMessage, ServerResponse } from 'http';
import solidPlugin from 'vite-plugin-solid';
import solid from "vite-plugin-solid";
import path from "path";
import { visualizer } from "rollup-plugin-visualizer";
import compress from "vite-plugin-compression";
import protoGRPCPlugin from './vite-plugin-proto-grpc';
import commonjs from '@rollup/plugin-commonjs';

const ClientSideRouting = {
  name: "dynamic-router",
  configureServer(server: ViteDevServer) {
    server.middlewares.use((req: IncomingMessage, res: ServerResponse, next: () => void) => {
      if (req.url?.search(/^\/@\d+/) !== -1) {
        req.url = "/";
      }
      next();
    });
  },
};

export default defineConfig({
  plugins: [
    solid(), // Поддержка SolidJS
	protoGRPCPlugin(),
	solidPlugin(),
    visualizer({ filename: "dist/bundle-analysis.html" }), // Анализ бандла
    compress({ algorithm: "gzip" }), // Gzip-сжатие
	ClientSideRouting,
	commonjs({})
  ],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src"),
    },
    extensions: [".js", ".jsx", ".ts", ".tsx", ".scss", ".css"],
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
    include: ['google-protobuf', 'grpc-web']
  },
  root: path.resolve(__dirname),
  publicDir: path.resolve(__dirname, 'static'),
  base: "/app",
  build: {
	target: 'esnext',
    outDir: "dist",
	emptyOutDir: true,
    sourcemap: true, // Source maps
	cssCodeSplit: true,
    minify: "terser", // Минификация
    rollupOptions: {
	  input: {
		index: path.resolve(__dirname, 'index.html'),
        main: path.resolve(__dirname, 'src', 'index.tsx')
      },
      output: {
		entryFileNames: 'js/[name].[hash].js', // Формат имени файла для основного бандла
        chunkFileNames: 'js/[name].[hash].js', // Формат имени файла для чанков
        assetFileNames: 'assets/[name].[hash].[ext]' // Формат имени файла для ассетов
      },
    },
  },
});
