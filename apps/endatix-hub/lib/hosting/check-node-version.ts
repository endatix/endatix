import packageJson from '@/package.json' assert { type: 'json' }
import semver from 'semver'

export interface PackageJson {
    engines?: {
        node?: string;
    };
}

function checkNodeVersion() {
    const nodeRuntimeVersion = process.version;
    const { engines } = packageJson as PackageJson;

    if (!engines || !engines.node) {
        console.log(getSuccessMessage(nodeRuntimeVersion));
        return;
    }

    if (!semver.satisfies(nodeRuntimeVersion, engines.node, { includePrerelease: true })) {
        console.log(getWarningMessage(nodeRuntimeVersion, engines.node));
    } else {
        console.log(getSuccessMessage(nodeRuntimeVersion));
    }
}

const getSuccessMessage = (nodeRuntimeVersion: string) => {
    return `📦 Node version is ${nodeRuntimeVersion}. Node version check passed ✅`;
}

const getWarningMessage = (nodeRuntimeVersion: string, engines: string) => {
    return `⚠️ Warning: Node version check failed ❌ 
            📦 Current Node version (${nodeRuntimeVersion}) does not match the required version of Node (${engines}). 
            💡 Check Readme for how to setup the correct Node version. 
            🔗 More info at https://github.com/endatix/endatix/tree/main/apps/endatix-hub`;
}

export default checkNodeVersion;