---
sidebar_position: 1
title: Endatix Hub
description: Endatix Hub is the Next.js form management UI — environment variables, storage, authentication, and deployment.
---

# Endatix Hub

Hub is the visual layer on the [Endatix API](/docs/developers/api/): form builder, submissions, and workspace admin. Next.js App Router, Auth.js, Tailwind. **Commercial license** for production.

Configure Hub with **environment variables**. The API uses `appsettings.json` — do not look for Hub `.env` keys under [Configuration → Settings](/docs/configuration/settings/).

## Start here

- [Hub environment variables](/docs/developers/hub/environment) — required keys, request-time `ENDATIX_*`, Helm
- [Environment](/docs/end-users/administration/environment) — Platform Admin runtime audit (no secret values)
- [Asset storage](/docs/configuration/asset-storage) — Azure or S3/RustFS for uploads
- [Authentication](/docs/building-your-solution/authentication/) — JWT, Keycloak, Google
- [Self-hosting](/docs/building-your-solution/deployment/self-hosting) · [Subfolder](/docs/building-your-solution/deployment/subfolder-deployment) · [Reverse proxy](/docs/building-your-solution/deployment/reverse-proxy-deployment)

Local clone: copy `hub/.env.example`, set the required secrets, run `pnpm dev`. Platform Admin → Environment shows the live request-time values.
