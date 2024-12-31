import packageJson from '@/package.json' assert { type: 'json' }
import semver from 'semver'

function checkNodeVersion() {
    const nodeRuntimeVersion = process.version;
    const { engines: { node: engines } } = packageJson;

    if (!semver.satisfies(nodeRuntimeVersion, engines, { includePrerelease: true })) {
        console.log(`⚠️ Warning: Node version check failed ❌ 
            📦 Current Node version (${nodeRuntimeVersion}) does not match the required version of Node (${engines}). 
            💡 Check Readme for how to setup the correct Node version. 
            🔗 More info at https://github.com/endatix/endatix/tree/main/apps/endatix-hub`);
    } else {
        console.log(`📦 Node version is ${nodeRuntimeVersion}. Node version check passed ✅`);
    }
}

export default checkNodeVersion;