import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiTarget = env.VITE_API_PROXY_TARGET || env.VITE_API_BASE_URL || 'http://localhost:5281'

  return {
    plugins: [
      react(),
      babel({ presets: [reactCompilerPreset()] }),
      tailwindcss(),
    ],
    resolve: {
      alias: {
        '@': path.resolve(__dirname, 'src'),
      },
    },
    server: {
      proxy: {
        '/queries': {
          target: apiTarget,
          changeOrigin: true,
          cookieDomainRewrite: '',
        },
        '/health': {
          target: apiTarget,
          changeOrigin: true,
          cookieDomainRewrite: '',
        },
        '/auth': {
          target: apiTarget,
          changeOrigin: true,
          cookieDomainRewrite: '',
        },
        '/admin': {
          target: apiTarget,
          changeOrigin: true,
          cookieDomainRewrite: '',
        },
      },
    },
  }
})
