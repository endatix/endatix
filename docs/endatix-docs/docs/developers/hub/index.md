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

## Hub developer pages

- [Embed events](/docs/developers/hub/embed-events) — postMessage contract for embedded forms
- [Azure Blob Storage](/docs/developers/hub/azure-storage) — configure Hub asset storage on Azure
- [RustFS storage](/docs/developers/hub/rustfs-storage) — S3-compatible RustFS
- [Asset storage overview](/docs/configuration/asset-storage) — public vs private mode
- [Maintenance mode](/docs/developers/hub/maintenance-mode) — env-driven Hub maintenance
- [Authentication](/docs/building-your-solution/authentication/) — JWT, Keycloak, Google
- [Self-hosting](/docs/building-your-solution/deployment/self-hosting) · [Subfolder](/docs/building-your-solution/deployment/subfolder-deployment) · [Reverse proxy](/docs/building-your-solution/deployment/reverse-proxy-deployment)

Local clone: copy `hub/.env.example`, set the required secrets, run `pnpm dev`. Platform Admin → Environment shows the live request-time values.
