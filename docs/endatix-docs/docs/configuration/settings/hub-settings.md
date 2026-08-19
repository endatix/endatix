---
sidebar_position: 4
title: API Hub URL
description: Point the Endatix API at your Hub origin so emails and exported file links open the right UI.
sidebar_label: API Hub URL
---

# API Hub URL

This is an **API** setting (`Endatix:Hub`). It tells the API where Hub lives so identity emails and some file links can open the UI.

It is **not** Hub’s own config. Hub origin, Auth.js, and the API URL Hub calls are [Hub environment variables](/docs/developers/hub/environment) (`AUTH_URL`, `ENDATIX_BASE_URL`, …).

## Configuration

```json
{
  "Endatix": {
    "Hub": {
      "HubBaseUrl": "https://your-endatix-hub.domain"
    }
  }
}
```

Bound to `Endatix.Core.Configuration.HubSettings` (`SectionName = "Endatix:Hub"`).

| Setting | Description | Default |
| --- | --- | --- |
| `HubBaseUrl` | Public Hub origin. Trailing slashes are ignored. Used in emails and export file links. | `""` |

If `HubBaseUrl` is empty, the API skips rewriting those links. Default hosting (`ConfigureEndatix()` / `ConfigureEndatixWithDefaults`) binds this automatically.

See also [Email Settings](/docs/configuration/settings/email-settings) for template from-addresses and Hub links inside mail.
