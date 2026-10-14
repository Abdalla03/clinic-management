import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import type { IncomingMessage, ServerResponse } from 'node:http'
import type { Plugin } from 'vite'
import { defineConfig } from 'vitest/config'

/**
 * Serves a local `/config.js` during `vite dev` and `vite preview` so the
 * runtime API-URL override (an empty string here) exists without nginx.
 * The production image ships a real config.js written by docker-entrypoint.sh.
 */
function serveConfigJs(): Plugin {
  const send = (_req: IncomingMessage, res: ServerResponse) => {
    res.setHeader('Content-Type', 'application/javascript')
    res.end("window.__CLINIC_API_URL__ = '';\n")
  }
  return {
    name: 'serve-config-js',
    configureServer(server) {
      server.middlewares.use('/config.js', send)
    },
    configurePreviewServer(server) {
      server.middlewares.use('/config.js', send)
    },
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss(), serveConfigJs()],
  server: {
    port: 3000,
  },
  preview: {
    port: 3000,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})