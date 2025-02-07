# Endatix Hub
This is the Endatix Hub, a Form Management system, built with Next.js. It uses the Endatix API to manage forms and submissions and offer data collection and reporting.

## System Requirements

- **Node.js 20.x.x** (Node 20.9.0 is recommended)
- Windows, Linux and MacOS are all supported

## Prerequisites

- **pnpm >=9.0.0** - we recommend using pnpm as the package manager for this project. You can install pnpm by running `npm install -g pnpm`
- **nvm** - we recommend using nvm to manage node versions as this will help you install the correct version of node without having to manually change the node version in your system. Download nvm [here](https://github.com/nvm-sh/nvm)

>[!TIP]
>If you are using nvm, you can install the correct version of node by running `nvm install v20.9.0`

## Getting Started

1. Setup correct node version. Open the terminal and run `nvm use v20.9.0`
2. Install the dependencies. Run `pnpm install`
3. Run the development server. We recommend using pnpm dev:

```bash
pnpm dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser to see the result. 

## Environment Variables

Check the .env.example file for all variables and their description

## Running Production Build Locally

This is useful for testing the production build locally (assumes you have run `pnpm install`)

1. Run `pnpm build:standalone`;
1. Run the site

```bash
# For localhost **without SSL certificate**
NODE_TLS_REJECT_UNAUTHORIZED=0 node .next/standalone/server.js

# For localhost **with SSL certificate**
node .next/standalone/server.js
```

## Learn More

To learn more about Endatix, take a look at the following resources:

- [Endatix Documentation](https://docs.endatix.com/docs/category/getting-started) - learn about Endatix features and API.
