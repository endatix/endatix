import { RemotePattern } from "next/dist/shared/lib/image-config";
import { includesRemoteImageHostnames } from "@/lib/hosting/next-config-helper";
import { describe, expect, it } from "vitest";

const wildcardRemotePattern: RemotePattern = {
  protocol: "https",
  hostname: "**",
};

describe("includesRemoteImageHostnames", () => {
  it("should do nothing if remotePatterns is undefined", () => {
    const remotePatterns: RemotePattern[] | undefined = undefined;

    includesRemoteImageHostnames(remotePatterns);

    expect(remotePatterns).toBeUndefined();
  });

  it("should add wildcard hostname if REMOTE_IMAGE_HOSTNAMES is empty", () => {
    process.env.REMOTE_IMAGE_HOSTNAMES = "";
    const remotePatterns: RemotePattern[] = [];

    includesRemoteImageHostnames(remotePatterns);

    expect(remotePatterns).toEqual([wildcardRemotePattern]);
  });

  it("should add wildcard hostname if REMOTE_IMAGE_HOSTNAMES is undefined", () => {
    delete process.env.REMOTE_IMAGE_HOSTNAMES;
    const remotePatterns: RemotePattern[] = [];

    includesRemoteImageHostnames(remotePatterns);

    expect(remotePatterns).toEqual([wildcardRemotePattern]);
  });

  it("should add multiple hostnames from comma-separated REMOTE_IMAGE_HOSTNAMES", () => {
    process.env.REMOTE_IMAGE_HOSTNAMES =
      "images.unsplash.com,images.pexels.com";
    const remotePatterns: RemotePattern[] = [];

    includesRemoteImageHostnames(remotePatterns);

    expect(remotePatterns).toEqual([
      {
        protocol: "https",
        hostname: "images.unsplash.com",
      },
      {
        protocol: "https",
        hostname: "images.pexels.com",
      },
    ]);
  });

  it("should handle single hostname in REMOTE_IMAGE_HOSTNAMES", () => {
    process.env.REMOTE_IMAGE_HOSTNAMES = "images.unsplash.com";
    const remotePatterns: RemotePattern[] = [];

    includesRemoteImageHostnames(remotePatterns);

    expect(remotePatterns).toEqual([
      {
        protocol: "https",
        hostname: "images.unsplash.com",
      },
    ]);
  });

  it("should trim empty strings and whitespace from hostnames", () => {
    process.env.REMOTE_IMAGE_HOSTNAMES =
      "  images.unsplash.com  ,  ,  images.pexels.com  ";
    const remotePatterns: RemotePattern[] = [];

    includesRemoteImageHostnames(remotePatterns);

    expect(remotePatterns).toEqual([
      {
        protocol: "https",
        hostname: "images.unsplash.com",
      },
      {
        protocol: "https",
        hostname: "images.pexels.com",
      },
    ]);
  });
});
