import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  output: "standalone", // Used to decrease the size of the application, check https://nextjs.org/docs/pages/api-reference/next-config-js/output
  async redirects() {
    return [
      {
        source: '/((?!.swa).*)about',
        destination: '/',
        permanent: true,
      },
    ];
  },
};

export default nextConfig;
