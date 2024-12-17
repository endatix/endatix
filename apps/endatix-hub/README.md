# Endatix Hub
This is the Endatix Hub, a Form Management system, built with Next.js. It uses the Endatix API to manage forms and submissions and offer data collection and reporting.

## Prerequisites

- **Node.js 20.9.0**
- **pnpm >=9.0.0**

## Getting Started

First, run the development server. We recommend using pnpm dev:

```bash
pnpm dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser to see the result. If you want to run the site in https, run `pnpm run dev-https` command.

This project uses [`next/font`](https://nextjs.org/docs/app/building-your-application/optimizing/fonts) to automatically optimize and load [Geist](https://vercel.com/font), a new font family for Vercel.

## Environment Variables

Check the .env.example file for all variables and their description

## Running Production Build Locally

This is useful for testing the production build locally (assumes you have run `pnpm install`)

1. Run `pnpm build:standalone`;
1. Run `pnpm start`;

## Learn More

To learn more about Next.js, take a look at the following resources:

- [Next.js Documentation](https://nextjs.org/docs) - learn about Next.js features and API.
- [Learn Next.js](https://nextjs.org/learn) - an interactive Next.js tutorial.

You can check out [the Next.js GitHub repository](https://github.com/vercel/next.js) - your feedback and contributions are welcome!

## Deploy on Vercel

The easiest way to deploy your Next.js app is to use the [Vercel Platform](https://vercel.com/new?utm_medium=default-template&filter=next.js&utm_source=create-next-app&utm_campaign=create-next-app-readme) from the creators of Next.js.

Check out our [Next.js deployment documentation](https://nextjs.org/docs/app/building-your-application/deploying) for more details.
