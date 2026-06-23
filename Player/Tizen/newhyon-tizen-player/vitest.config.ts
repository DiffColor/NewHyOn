import { defineConfig } from 'vitest/config';
import { fileURLToPath, URL } from 'node:url';

const rootDir = fileURLToPath(new URL('./', import.meta.url));

export default defineConfig({
  root: rootDir,
  test: {
    environment: 'jsdom',
    include: ['tests/**/*.test.ts'],
    env: {
      NEWHYON_TIZEN_PLAYER_ROOT: rootDir,
    },
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
});
