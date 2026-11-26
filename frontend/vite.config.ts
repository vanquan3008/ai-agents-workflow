/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // Biến không có tiền tố VITE_ chỉ dùng lúc build/dev server, không vào bundle.
  const env = loadEnv(mode, process.cwd(), '')
  const proxyTarget = env.DEV_PROXY_TARGET || 'http://localhost:5080'

  return {
    plugins: [react()],
    server: {
      port: Number(env.DEV_SERVER_PORT || 5173),
      strictPort: true,
      // Dev: gọi cùng origin, Vite chuyển tiếp tới API để tránh CORS và cookie khác site.
      proxy: {
        '/api': { target: proxyTarget, changeOrigin: true },
        '/hubs': { target: proxyTarget, changeOrigin: true, ws: true },
        '/health': { target: proxyTarget, changeOrigin: true },
      },
    },
    build: {
      sourcemap: true,
      target: 'es2023',
    },
    test: {
      environment: 'jsdom',
      globals: false,
      setupFiles: ['./src/test/setup.ts'],
      css: false,
      env: {
        VITE_AUTH_MODE: 'development',
        VITE_DEV_USER_ID: '0193a1c0-0000-7000-8000-000000000001',
      },
    },
  }
})
