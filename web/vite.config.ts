/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// Backend origin for the dev proxy. The browser only ever calls relative /api URLs,
// so the dev server forwards them. Never call localhost from browser code.
const apiOrigin = process.env.EDM_API_ORIGIN ?? 'http://localhost:5000';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    host: '0.0.0.0',
    port: 5173,
    strictPort: true,
    // Allow the sandbox preview host (for example 5173-<sandbox>.e2b.app) and localhost only.
    allowedHosts: ['.e2b.app', 'localhost', '127.0.0.1'],
    proxy: {
      '/api': { target: apiOrigin, changeOrigin: true },
    },
  },
  preview: {
    host: '0.0.0.0',
    port: 4173,
    strictPort: true,
    allowedHosts: ['.e2b.app', 'localhost', '127.0.0.1'],
  },
  build: {
    sourcemap: true,
    target: 'es2022',
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    css: false,
  },
});
