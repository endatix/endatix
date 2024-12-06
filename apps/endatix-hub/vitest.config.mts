import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tsconfigPaths from 'vite-tsconfig-paths'

export default defineConfig({
  plugins: [tsconfigPaths(), react()],
  test: {
    environment: 'jsdom',
    coverage: {
      include: [
        'app/**/*.{ts,tsx}',
        'actions/**/*.ts',
        'components/**/*.tsx',
        'lib/**/*.{ts,tsx}',
        'services/**/*.ts',
        'types/**/*.ts',
      ],
      exclude: [
        'node_modules/',
        '**/*.d.ts',
        'components/ui/**/*.tsx',
      ],
      enabled: true,
      provider: 'v8',
      reporter: ['text','cobertura'],
      reportsDirectory: '../../.coverage/endatix-hub',
    },
  },
})