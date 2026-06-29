---
sidebar_position: 6
title: Storage Settings
description: Configure Endatix external file storage - Azure Blob Storage and S3-compatible services — with all supported settings and environment variables.
---

# Configuring Storage Settings in Endatix

Storage settings configure external storage providers used by Endatix integrations.

Today, these settings drive:

- **Export URL rewriting** for submission file answers (storage URLs → Hub file-details URLs)
- **Submission file URL fetch policy** when staff download submission files via the OSS API (`POST forms/{formId}/submissions/{submissionId}/files`)

Both features use the same Azure Blob provider keys: `HostName` and `UserFilesContainerName`.

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

Use the **public** blob hostname that Hub stores in submission JSON (not a Private Link DNS name unless that is what respondents see in URLs).

### Submission file URL fetch (SSRF mitigation)

When staff download submission files, the API may re-fetch file `content` URLs stored in submission JSON. To prevent server-side request forgery ([GHSA-jw28-j36r-gp9m](https://github.com/endatix/endatix/security/advisories/GHSA-jw28-j36r-gp9m)), URL fetches are **fail-closed** unless they match:

- The configured `HostName` (must match the host in stored submission URLs)
- The canonical path `{UserFilesContainerName}/s/{formId}/{submissionId}/{fileName}` for the current request
- `http` or `https` only, with no credentials in the URL
- HTTP redirects are not followed (`AllowAutoRedirect = false`)

Inline `data:` URIs and raw base64 content in JSON are unaffected.

**Azure Blob not configured:** all URL fetches are rejected; `data:` and base64 downloads still work.

#### Network-layer controls (recommended)

Application host/path checks stop arbitrary attacker URLs. **Outbound network policy** at the platform layer provides defense in depth and supports Azure Private Link (where the public hostname may resolve to private addresses inside your VNet).

Configure egress so the API workload can reach only legitimate destinations, for example:

- Azure Storage service tags or known blob IP ranges for your region
- Your storage account’s public endpoint, or Private Endpoint subnets when using Private Link
- Deny RFC1918, loopback, and link-local targets (including `169.254.169.254` metadata) except where explicitly required for platform health

Examples: Azure Firewall application rules, NSG egress restrictions, or equivalent WAF/egress controls in your hosting environment.

Misconfiguration of `HostName` (e.g. pointing at an internal host) is an operator error; combine correct storage config with restricted egress.

Multi-provider storage (S3/RustFS) and API-served storage config are planned — see [endatix#823](https://github.com/endatix/endatix/issues/823).

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

## Follow-up

Long-term, OSS will list and download submission files directly from the configured storage provider (`s/{formId}/{submissionId}/`), removing the need for JSON URL HTTP-fetch. See [endatix#823](https://github.com/endatix/endatix/issues/823).
