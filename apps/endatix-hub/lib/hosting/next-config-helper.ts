import { RemotePattern } from "next/dist/shared/lib/image-config";

export function includesRemoteImageHostnames(
  remotePatterns: RemotePattern[] | undefined
): void {
  if (!remotePatterns) {
    return;
  }

  const REMOTE_IMAGE_HOSTNAMES =
    process.env.REMOTE_IMAGE_HOSTNAMES?.trim() ?? "";
  const hostnames = REMOTE_IMAGE_HOSTNAMES.split(",").map((h: string) => h.trim());

  if (!REMOTE_IMAGE_HOSTNAMES || hostnames.length < 1) {
    remotePatterns.push({
      protocol: "https",
      hostname: "**",
    });
    return;
  }

  hostnames.forEach((hostname: string) => {
    if (!hostname?.length) {
      return;
    }

    remotePatterns.push({
      protocol: "https",
      hostname: hostname,
    });
  });
}
