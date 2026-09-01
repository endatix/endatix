---
sidebar_position: 2
title: Auth Settings
---

# Auth Settings

These are **Endatix API** JWT settings (`Endatix:Auth`). They sign tokens the API issues.

Hub session cookies and Keycloak/Google client env live in [Hub environment variables](/docs/developers/hub/environment). SSO walkthroughs: [Authentication](/docs/building-your-solution/authentication/).

:::warning[Security Note]

:lock: Never expose `SigningKey` in public files or client-side code. Store it in environment variables or a secrets manager.

:::

## Configuration

To configure JWT settings, add the following snippet to your appSettings.json file. Ensure the SigningKey and other sensitive values are securely stored:

<details>
<summary>Note on migrating from versions prior to [0.4.2](https://github.com/endatix/endatix/releases/tag/v0.4.2)</summary>

Before the migration, the settings were defined under the `"Jwt": {...}` section as below:

```json
{
  "Endatix": {
    "Jwt": {
      "SigningKey": "your-secure-signing-key",
      "ExpiryInMinutes": 60,
      "Issuer": "endatix-api",
      "Audiences": ["endatix-hub"]
    }
  }
}
```

You need to migrate to the new Endatix:Auth:Providers:EndatixJwt section as below:
- `SigningKey` -> `SigningKey`
- `ExpiryInMinutes` -> `AccessExpiryInMinutes` :bulb: rename it to AccessExpiryInMinutes
- `Issuer` -> `Issuer`
- `Audiences` -> `Audiences`

</details>

```json
 "Endatix": {
  "Auth": {
    "Providers": {
      "EndatixJwt": {
        "SigningKey": "your-secure-signing-key",
        "AccessExpiryInMinutes": 15,
        "RefreshExpiryInDays": 7,
        "Issuer": "endatix-api",
        "ReBacIssuer": "edx_res_auth",
        "Audiences": ["endatix-hub"]
      }
    }
  }
}
```

## Explanation

The settings are defined under the `"Endatix" > "Auth" > "Providers" > "EndatixJwt"` section:

- **SigningKey:** The key used to sign the JWT token. This key must remain confidential and be stored securely (e.g., in environment variables). Exposing this key can compromise the entire authentication system.
  - :bulb: Tip: You can use the following command: `openssl rand -hex 32` to generate one.
  - example: `a28110cc8b94c5f3b3c923aa2c9fae4ed50c86ec61debfe6edb3c29947dbb00c`
- **AccessExpiryInMinutes:** Defines the lifetime of the JWT access token in minutes. The default value is 15 minutes, but you can adjust it to suit your security requirements.
  - :bulb: Tip: Shorter expiration times reduce the risk of token misuse. Consider using 15 minutes for production environments
  - example: `15`
- **RefreshExpiryInDays:** Defines the lifetime of the JWT refresh token in days. The default value is 7 days, allowing users to obtain new access tokens without re-authentication.

  - :bulb: Tip: Refresh tokens should have longer expiration times than access tokens to balance security and user experience
  - example: `7`

- **Issuer** Specifies the valid issuer of the JWT token. This value should match the authority responsible for generating tokens (e.g., your API's domain).

  - example: `"api.myapp.com"` or `"https://localhost:5000"` or `"endatix-api"`

- **ReBacIssuer:** Issuer claim (`iss`) for short-lived form-access (ReBAC) JWTs used on public data-list routes. Must differ from **Issuer** so the API can route bearer tokens to the correct validation handler.

- **Audiences:** Shared audience list for both user and ReBAC JWTs. Each audience represents an application or client that can use these JWTs against your API. The default audience is "endatix-hub", but you can add more as needed.
  - example: `["www.myapp.com", "https://localhost:3000", "my-endatix-app"]`
