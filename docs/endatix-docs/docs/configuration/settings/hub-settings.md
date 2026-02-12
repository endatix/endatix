---
sidebar_position: 4
title: Hub Settings
---

# Configuring Hub Settings in Endatix

Hub settings define how Endatix should link back to the Endatix Hub application (the UI). This is important for sending emails or generating links from the Endatix API to the Endatix Hub application

## Configuration

Configure Hub settings under the `Endatix:Hub` section:

```json
{
  "Endatix": {
    "Hub": {
      "HubBaseUrl": "https://your-endatix-hub.domain"
    }
  }
}
```

This section is bound to `Endatix.Core.Configuration.HubSettings` (`HubSettings.SectionName = "Endatix:Hub"`).

## Settings Reference

| Setting | Description | Default |
|---------|-------------|---------|
| `HubBaseUrl` | Base URL of the Endatix Hub (e.g. `https://your-endatix-hub.domain`). Used to generate links back to the Hub (e.g. emails, exported file links). Trailing slashes are ignored. | `""` |

## Notes

- If `HubBaseUrl` is empty, features that generate Hub links will **skip link rewriting** and keep original values where applicable.
- When using the default hosting setup (`builder.Host.ConfigureEndatix()` / `ConfigureEndatixWithDefaults(...)`) the options are bound automatically via ASP.NET Core configuration.

