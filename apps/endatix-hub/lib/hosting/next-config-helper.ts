import { RemotePattern } from "next/dist/shared/lib/image-config";

function includesRemoteImageHostnames(
  remotePatterns: RemotePattern[] | undefined,
): void {
  if (!remotePatterns) {
    return;
  }

  const REMOTE_IMAGE_HOSTNAMES =
    process.env.REMOTE_IMAGE_HOSTNAMES?.trim() ?? "";
  const hostnames = REMOTE_IMAGE_HOSTNAMES.split(",").map((h: string) =>
    h.trim(),
  );

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

/**
 * Generates a rewrite rule for parallel route segments in Next.js static chunks.
 * This is needed because Next.js encodes @ symbols in parallel route segments as %40 in file paths,
 * but the original URL patterns use @ symbol.
 * more info on the workaround here - https://github.com/vercel/next.js/issues/71626
 * @param parallelRouteRoot - The root name of the parallel route segment (without the @ symbol)
 * @returns A rewrite rule object with source and destination patterns, or null if invalid input
 */
function getRewriteRuleFor(parallelRouteRoot: string): {
  source: string;
  destination: string;
} {
  // Sanitize input by trimming whitespace
  const trimmedRouteRoot = parallelRouteRoot?.trim()?.toLowerCase();

  // Validate that route name exists and doesn't already start with @
  const isValidRouteName = Boolean(
    trimmedRouteRoot?.length > 0 && !trimmedRouteRoot.startsWith("@"),
  );

  if (!isValidRouteName) {
    throw new Error("Invalid route name value", {
      cause: parallelRouteRoot
    });
  }

  return {
    source: `/_next/static/chunks/app/:folder*/@${trimmedRouteRoot}/:path*`,
    destination: `/_next/static/chunks/app/:folder*/%40${trimmedRouteRoot}/:path*`,
  };
}

export { includesRemoteImageHostnames, getRewriteRuleFor };
