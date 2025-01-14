import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tsconfigPaths from 'vite-tsconfig-paths'

export default defineConfig({
  plugins: [tsconfigPaths(), react()],
  test: {
    globals: true,
    environment: 'jsdom',
    coverage: {
      include: [
        'features/**/*.ts',
        'app/**/*.{ts,tsx}',
        'components/**/*.tsx',
        'lib/**/*.{ts,tsx}',
        'services/**/*.ts',
        'types/**/*.ts',
      ],
      exclude: [
        'node_modules/',
        '**/*.d.ts',
        '**/index.ts',
        '**/*__tests__/**',
        'components/ui/**/*.tsx',
      ],
      enabled: true,
      provider: 'v8',
      reporter: process.env.GITHUB_ACTIONS ? ['cobertura', 'json', 'json-summary'] : ['cobertura', 'html'],
      reportsDirectory: '../../.coverage/endatix-hub',
    },
    reporters: process.env.GITHUB_ACTIONS ? ['dot', 'github-actions'] : ['dot']
  },
})