import { checkNodeVersion } from '@/lib/hosting/check-node-version';
import { describe, it, expect, vi, beforeEach, afterEach, MockInstance } from 'vitest';
import { PackageJson } from '@/lib/hosting/check-node-version';

const mockPackageJson: PackageJson = vi.hoisted(() => {
    return {
        engines: {
            node: '>=20.0.0 <21.0.0'
        }
    };
});

vi.mock('@/package.json', () => ({
    default: mockPackageJson
}));

describe('checkNodeVersion', () => {
    let consoleSpy: MockInstance;
    const originalVersion = process.version;
    const DEFAULT_REQUIRED_NODE_VERSION = '>=20.0.0 <21.0.0';

    const messages = {
        success: (version: string) => `Node version is ${version}. Node version check passed ✅`,
        warning: {
            header: '⚠️ Warning: Node version check failed ❌',
            versionMismatch: (current: string) =>
                `Current Node version (${current}) does not match the required version of Node (${DEFAULT_REQUIRED_NODE_VERSION})`,
            readmeInfo: '💡 Check Readme for how to setup the correct Node version',
            moreInfo: '🔗 More info at https://github.com/endatix/endatix/tree/main/apps/endatix-hub'
        }
    };

    const assertWarningMessage = (version: string) => {
        expect(consoleSpy).toHaveBeenCalledWith(
            expect.stringContaining(messages.warning.header)
        );
        expect(consoleSpy).toHaveBeenCalledWith(
            expect.stringContaining(messages.warning.versionMismatch(version))
        );
        expect(consoleSpy).toHaveBeenCalledWith(
            expect.stringContaining(messages.warning.readmeInfo)
        );
        expect(consoleSpy).toHaveBeenCalledWith(
            expect.stringContaining(messages.warning.moreInfo)
        );
    };

    const assertSuccessMessage = (version: string) => {
        expect(consoleSpy).toHaveBeenCalledWith(
            expect.stringContaining(messages.success(version))
        );
    };

    const setRuntimeNodeVersion = (version: string) => {
        Object.defineProperty(process, 'version', { value: version });
    };

    beforeEach(() => {
        consoleSpy = vi.spyOn(console, 'log').mockImplementation(() => { });
        vi.resetModules();
    });

    afterEach(() => {
        consoleSpy.mockRestore();
        setRuntimeNodeVersion(originalVersion);
        mockPackageJson.engines = { node: DEFAULT_REQUIRED_NODE_VERSION };
        vi.clearAllMocks();
    });

    it('should log success message when node version matches requirements', () => {
        // Arrange
        const validVersion = '20.9.0';
        setRuntimeNodeVersion(validVersion);

        // Act
        checkNodeVersion();

        // Assert
        assertSuccessMessage(validVersion);
    });

    it('should log warning when node version is lower than required', () => {
        // Arrange
        const invalidVersion = '14.0.0';
        setRuntimeNodeVersion(invalidVersion);

        // Act
        checkNodeVersion();

        // Assert
        assertWarningMessage(invalidVersion);
    });

    it('should log warning when node version is higher than required range', () => {
        // Arrange
        const futureVersion = '24.0.0';
        setRuntimeNodeVersion(futureVersion);

        // Act
        checkNodeVersion();

        // Assert
        assertWarningMessage(futureVersion);
    });

    it('should handle valid prerelease versions correctly', () => {
        // Arrange
        const prereleaseVersion = '20.10.0-rc.1';
        setRuntimeNodeVersion(prereleaseVersion);

        // Act
        checkNodeVersion();

        // Assert
        assertSuccessMessage(prereleaseVersion);
    });

    it('should handle invalid futureprerelease versions correctly', () => {
        // Arrange
        const prereleaseVersion = '22.10.0-rc.1';
        setRuntimeNodeVersion(prereleaseVersion);

        // Act
        checkNodeVersion();

        // Assert
        assertWarningMessage(prereleaseVersion);
    });

    it('should include helpful information in warning message', () => {
        // Arrange
        const invalidVersion = '14.0.0';
        setRuntimeNodeVersion(invalidVersion);

        // Act
        checkNodeVersion();

        // Assert
        assertWarningMessage(invalidVersion);
    });

    it('should handle valid fixed version requirement correctly', () => {
        // Arrange
        const exactVersion = '22.0.1';
        mockPackageJson.engines = { node: exactVersion };
        setRuntimeNodeVersion(exactVersion);

        // Act
        checkNodeVersion();

        // Assert
        assertSuccessMessage(exactVersion);
    });

    it('should handle no engines in package.json', () => {
        // Arrange
        mockPackageJson.engines = undefined;
        setRuntimeNodeVersion(originalVersion);

        // Act
        checkNodeVersion();

        // Assert
        assertSuccessMessage(originalVersion);
    });

    it('should handle no engines.node in package.json', () => {
        // Arrange
        mockPackageJson.engines = { node: undefined };
        setRuntimeNodeVersion(originalVersion);

        // Act
        checkNodeVersion();

        // Assert
        assertSuccessMessage(originalVersion);
    });
});
