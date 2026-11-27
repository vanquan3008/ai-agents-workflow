/// <reference types="vitest/config" />
import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // Variables without the VITE_ prefix are only visible to the build/dev server, never to the bundle.
  const env = loadEnv(mode, process.cwd(), '')
  const proxyTarget = env.DEV_PROXY_TARGET || 'http://localhost:5080'

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: { '@': path.resolve(import.meta.dirname, './src') },
    },
    server: {
      port: Number(env.DEV_SERVER_PORT || 5173),
      strictPort: true,
      // Same-origin calls in dev: Vite proxies to the API, avoiding CORS and cross-site cookies.
      proxy: {
        '/api': { target: proxyTarget, changeOrigin: true },
        '/hubs': { target: proxyTarget, changeOrigin: true, ws: true },
        '/health': { target: proxyTarget, changeOrigin: true },
      },
    },
    build: {
      target: 'es2023',
    },
    test: {
      environment: 'jsdom',
      globals: false,
      setupFiles: ['./src/test/setup.ts'],
      css: false,
    },
  }
})
