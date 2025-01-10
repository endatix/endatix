import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  output: "standalone", // Used to decrease the size of the application, check https://nextjs.org/docs/pages/api-reference/next-config-js/output
  reactStrictMode: true,
  experimental: {
    serverActions: {
      bodySizeLimit: '20mb',
    }
  },
  images: {
    remotePatterns: [
      {
        protocol: "https",
        hostname: "images.unsplash.com",
      },
      {
        protocol: "https",
        hostname: "endatixstorageqad.blob.core.windows.net",
      },
    ],
  },
};

export default nextConfig;
