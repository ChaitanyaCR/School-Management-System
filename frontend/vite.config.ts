import path from 'node:path';
import tailwindcss from '@tailwindcss/vite';
import { tanstackRouter } from '@tanstack/router-plugin/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// In development the API runs separately (dotnet run); Vite proxies API calls to it so the
// browser sees a single origin, exactly as Caddy does in production.
const apiTarget = process.env.API_URL ?? 'http://localhost:5080';

export default defineConfig({
  plugins: [tanstackRouter({ target: 'react', autoCodeSplitting: true }), react(), tailwindcss()],
  resolve: {
    alias: { '@': path.resolve(import.meta.dirname, 'src') },
  },
  server: {
    port: 5173,
    strictPort: true,
    // Tenants are addressed by subdomain, e.g. http://demo.localhost:5173
    allowedHosts: ['.localhost'],
    proxy: {
      '/api': apiTarget,
      '/health': apiTarget,
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
});
