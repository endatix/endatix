---
sidebar_position: 6
title: Storage Settings
---

# Configuring Storage Settings in Endatix

Storage settings configure external storage providers used by Endatix integrations.

Today, the primary use of these settings is **export URL rewriting** for submission file answers:
- Storage URLs are detected using provider configuration (e.g. Azure Blob host + container)
- Matching URLs can be rewritten into Hub file-details URLs during export

## Configuration

Storage is provider-based and configured under `Endatix:Storage:Providers`.

This section is bound to `Endatix.Infrastructure.Storage.StorageOptions` (`StorageOptions.SectionName = "Endatix:Storage"`).

### Azure Blob provider (current)

```json
{
  "Endatix": {
    "Storage": {
      "Providers": {
        "AzureBlob": {
          "HostName": "myaccount.blob.core.windows.net",
          "UserFilesContainerName": "secure-vault"
        }
      }
    }
  }
}
```

## Settings Reference

### `Endatix:Storage`

| Setting | Description | Default |
|---------|-------------|---------|
| `Providers` | Provider configuration dictionary. Each key is a provider name (e.g. `AzureBlob`). | `{}` |

### `Endatix:Storage:Providers:AzureBlob`

| Setting | Description | Default |
|---------|-------------|---------|
| `HostName` | Storage account host name **without scheme** (e.g. `account.blob.core.windows.net` or a custom domain). | `""` |
| `UserFilesContainerName` | Container name used for submission files (path convention: `{container}/s/{formId}/{submissionId}/{fileName}`). | `"user-files"` |

## Notes

- If either `HostName` or `UserFilesContainerName` is empty, Azure Blob storage URL detection is considered **not configured**, and export URL rewriting will be disabled.
- When using the default hosting setup (`builder.Host.ConfigureEndatix()` / `ConfigureEndatixWithDefaults(...)`), external storage options are registered automatically.

