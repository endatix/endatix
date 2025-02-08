import type { NextConfig } from "next";
import {
  getRewriteRuleFor,
  includesRemoteImageHostnames,
} from "./lib/hosting/next-config-helper";
import { StorageService } from "@/features/storage/infrastructure/storage-service";
import { Rewrite } from "next/dist/lib/load-custom-routes";

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
    remotePatterns: [],
  },
  rewrites: async () => {
    const rules = {
      beforeFiles: new Array<Rewrite>(),
      afterFiles: new Array<Rewrite>(),
      fallback: new Array<Rewrite>(),
    };

    const headerRouteFix = getRewriteRuleFor("header");
    rules.beforeFiles.push(headerRouteFix);

    return rules;
  },
};

includesRemoteImageHostnames(nextConfig.images?.remotePatterns);

const storageConfig = StorageService.getAzureStorageConfig();
if (storageConfig.isEnabled) {
  nextConfig?.images?.remotePatterns?.push({
    protocol: "https",
    hostname: storageConfig.hostName,
  });
}

export default nextConfig;
