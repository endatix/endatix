import type { NextConfig } from "next";
import { StorageService } from "@/features/storage/infrastructure/storage-service";

const nextConfig: NextConfig = {
  /* config options here */
  output: "standalone", // Used to decrease the size of the application, check https://nextjs.org/docs/pages/api-reference/next-config-js/output
  reactStrictMode: true,
  experimental: {
    serverActions: {
      bodySizeLimit: "20mb",
    },
  },
  images: {
    remotePatterns: [
      {
        protocol: "https",
        hostname: process.env.IMAGE_HOSTNAME || "images.unsplash.com",
      },
    ],
  },
};

const storageConfig = StorageService.getAzureStorageConfig();
if (storageConfig.isEnabled) {
  nextConfig?.images?.remotePatterns?.push({
    protocol: "https",
    hostname: storageConfig.hostName,
  });
}

export default nextConfig;
