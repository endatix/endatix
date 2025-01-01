import checkNodeVersion from './lib/hosting/check-node-version'

export function register() {
    checkNodeVersion();
}