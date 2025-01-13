import { describe, it, expect } from 'vitest';
import { isNodeRuntime, isEdgeRuntime } from '@/lib/hosting/runtime';

describe('runtime', () => {
    describe('isNodeRuntime', () => {
        it('should return true when NEXT_RUNTIME is nodejs', () => {
            // Arrange
            process.env.NEXT_RUNTIME = 'nodejs';

            // Act
            const result = isNodeRuntime();

            // Assert
            expect(result).toBe(true);
        });

        it('should return false when NEXT_RUNTIME is not nodejs', () => {
            // Arrange
            process.env.NEXT_RUNTIME = 'edge';

            // Act
            const result = isNodeRuntime();

            // Assert
            expect(result).toBe(false);
        });
    });

    describe('isEdgeRuntime', () => {
        it('should return true when NEXT_RUNTIME is edge', () => {
            // Arrange
            process.env.NEXT_RUNTIME = 'edge';

            // Act
            const result = isEdgeRuntime();

            // Assert
            expect(result).toBe(true);
        });

        it('should return false when NEXT_RUNTIME is not edge', () => {
            // Arrange
            process.env.NEXT_RUNTIME = 'nodejs';

            // Act
            const result = isEdgeRuntime();

            // Assert
            expect(result).toBe(false);
        });

        it('should return false when NEXT_RUNTIME is undefined', () => {
            // Arrange
            process.env.NEXT_RUNTIME = undefined;

            // Act
            const result = isEdgeRuntime();

            // Assert
            expect(result).toBe(false);
        });
    });
});
