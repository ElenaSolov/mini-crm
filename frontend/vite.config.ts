import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The dev server forwards /api to the backend, so the browser sees one origin
// and the API needs no CORS setup.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5080',
    },
  },
});
