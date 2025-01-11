import checkNodeVersion from './lib/hosting/check-node-version'

export const register = async () => {
  checkNodeVersion();
  if (process.env.NEXT_RUNTIME === "nodejs") {
    await import("./instrumentation.node");
  }
};
