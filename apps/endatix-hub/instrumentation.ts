export const register = async () => {
  if (process.env.NEXT_RUNTIME === 'nodejs') {
    await import("@/lib/hosting/check-node-version");
    await import("@/instrumentation.node");
  }
};
