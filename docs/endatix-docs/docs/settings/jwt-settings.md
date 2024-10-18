---
sidebar_position: 2
title: JWT Settings
---

# Configuring JWT Settings in Endatix

JWT (JSON Web Token) settings control the authentication and authorization mechanisms in Endatix. These settings are essential for securing your API and client interactions, ensuring that only authorized users can access protected resources.

:::warning Security Note

:lock: Never expose sensitive JWT settings, like the SigningKey, in public configuration files or the client-side code. Exposing these keys is a security vulnerability that can lead to token forgery, data breaches, or unauthorized access. Always store them securely in environment variables or a dedicated secrets manager.

:::

# Setting Thing Up

To configure JWT settings, add the following snippet to your appSettings.json file. Ensure the SigningKey and other sensitive values are securely stored:

```json
 "Endatix": {
  "Jwt": {
    "SigningKey": "your-secure-signing-key",
    "ExpiryInMinutes": 60,
    "Issuer": "endatix-api",
    "Audiences": ["endatix-hub"]
  }
}
```

# Explanation

The settings are defined under the `"Jwt": {...}` section:

- **SigningKey:** The key used to sign the JWT token. This key must remain confidential and be stored securely (e.g., in environment variables). Exposing this key can compromise the entire authentication system.
  - :bulb: Tip: You can use the following command: `openssl rand -hex 32` to generate one. 
  - example: `a28110cc8b94c5f3b3c923aa2c9fae4ed50c86ec61debfe6edb3c29947dbb00c`
- **ExpiryInMinutes:** Defines the lifetime of the JWT token. The default value is 60 minutes, but you can adjust it to suit your security requirements. 
  - :bulb: Tip: Shorter expiration times reduce the risk of token misuse. You can set this on prod to 15 minutes
  - example: `15`

- **Issuer** Specifies the valid issuer of the JWT token. This value should match the authority responsible for generating tokens (e.g., your API's domain).
  - example: `"api.myapp.com"` or `"https://localhost:5000"` or `"endatix-api"`

- **Audiences:** A list of valid audiences that can receive the token. Each audience represents an application or client that is allowed to use the JWT for - authentication. The default audience is "endatix-hub", but you can add more as needed.
  - example: `["www.myapp.com", "https://localhost:3000", "my-endatix-app"]`
