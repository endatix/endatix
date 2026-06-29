---
sidebar_position: 2
title: Maintenance mode
description: Enable read-only maintenance UX for the Endatix Hub browser UI using env-driven proxy behavior and the /maintenance page.
---

import useBaseUrl from "@docusaurus/useBaseUrl";
import ThemedImage from "@theme/ThemedImage";
import maintenanceScreenshotLight from "./maintenance_light.png";
import maintenanceScreenshotDark from "./maintenance_dark.png";

# Hub maintenance mode

Endatix Hub supports a **single-deployment** maintenance experience controlled by environment variables. When enabled, the Next.js [`proxy`](https://nextjs.org/docs/app/api-reference/file-conventions/proxy) layer (implemented as `proxy.ts` in the Hub repo) **rewrites** matched browser requests to the `/maintenance` route and returns **HTTP 503** with an optional **`Retry-After`** header.

This is intended for **planned work** or **degraded UI** scenarios while keeping configuration simple and **avoiding extra runtime dependencies** (no database or remote flag store in this version). Another advantage is that it's integrated into the Endatix Hub, so you don't have to use additional infrastructure e.g. edge compute, CDN, WAF. If you already have dedicate maintenance page, solution you will probably want to use it instead of this one, where the focus is ease of use.

<ThemedImage
alt="Maintenance page screenshot"
sources={{
    light: useBaseUrl(maintenanceScreenshotLight),
    dark: useBaseUrl(maintenanceScreenshotDark),
  }}
/>

## User-visible behavior

- **Matched routes:** Requests that hit the proxy matcher are **rewritten internally** to `/maintenance`. The address bar typically **still shows the original path** (rewrite, not redirect). The response status is **503** so monitors and bots can treat the Hub UI as temporarily unavailable.
- **Direct `/maintenance`:** The `/maintenance` path is **excluded** from the matcher so the maintenance page can render without looping.
- **Static assets:** Paths such as `/_next/static`, `/_next/image`, `favicon.ico`, and files under `assets` are **excluded** so CSS, JS chunks, and icons keep loading for the maintenance page.

## API routes

The proxy **`matcher` excludes `/api/*`**. Therefore:

- **REST `/api` routes are not gated by Hub maintenance mode.** Integrations, mobile clients, and server-to-server callers **continue to hit the Hub’s API routes** as before unless the API process or upstream backend is down separately.
- If you need **503 JSON for every API** during a full outage, that requires a **different** policy (for example extending the matcher or adding shared handling in route handlers). That is **not** part of the default Hub maintenance behavior.

This split keeps the implementation small and avoids surprising API consumers during UI-only maintenance.

## Environment variables

### Toggle and HTTP hints

| Variable                          | Required | Description                                                                                                               |
| --------------------------------- | -------- | ------------------------------------------------------------------------------------------------------------------------- |
| `MAINTENANCE_MODE`                | No       | Set to `true` to enable maintenance. Any other value or absence means normal operation.                                   |
| `MAINTENANCE_RETRY_AFTER_SECONDS` | No       | If set to a valid non-negative integer, sent as the **`Retry-After`** response header (seconds) on maintenance responses. |

### Page Data (all optional; defaults ship in code)

Defaults are generic (“scheduled maintenance”, no promotional URLs or migration messaging). Override these if you need locale- or tenant-specific wording.

| Variable                           | Description                                   |
| ---------------------------------- | --------------------------------------------- |
| `MAINTENANCE_BADGE_LABEL`          | Short badge text (for example “Maintenance”). |
| `MAINTENANCE_TITLE`                | Main heading.                                 |
| `MAINTENANCE_CARD_DESCRIPTION`     | Subtitle under the heading.                   |
| `MAINTENANCE_BODY`                 | Primary paragraph.                            |
| `MAINTENANCE_FOOTER`               | Closing line (for example a thank-you).       |
| `MAINTENANCE_METADATA_TITLE`       | HTML `<title>` / metadata title.              |
| `MAINTENANCE_METADATA_DESCRIPTION` | Meta description for SEO and previews.        |

## HTTP status and Next.js

The intended response is **503** plus optional **`Retry-After`**. Behavior is implemented with `NextResponse.rewrite` and `status: 503`. If you observe different status codes in a specific hosting setup, record the Next.js version and document the actual behavior for your environment.

The `/maintenance` route sets **`robots`: noindex, nofollow** in metadata so transient maintenance copy is less likely to be indexed when users open `/maintenance` directly.

## Operations checklist

1. Set `MAINTENANCE_MODE=true` (and optional copy variables) in the .env or your pipeline’s variable group.
2. **Restart** the App Service (or redeploy) so the Node process reads the new settings.
3. Verify in a browser that hub routes show the maintenance experience with **503** (for example via DevTools Network).
4. Confirm whether **API callers** should still succeed under Option A; plan gateway or API changes separately if you need a full outage response for APIs.
5. To restore service, set `MAINTENANCE_MODE` to `false` or remove it, then restart again.

## Troubleshooting

| Symptom                   | Likely cause                                                                                                                                         |
| ------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| Maintenance never appears | Typo in variable name; value not exactly `true`; app not restarted; request path excluded by matcher (for example `/api/...`).                       |
| Broken styling / no icons | Rare if static exclusions match your hosting paths; confirm `/_next/static` and `favicon.ico` are not accidentally routed through maintenance logic. |
| APIs still respond 200    | **Expected** under Option A — `/api` is excluded from the maintenance matcher by design.                                                             |

## Related documentation

- [Endatix Hub overview](./index.md)
- [Hub settings](../../configuration/settings/hub-settings.md)
