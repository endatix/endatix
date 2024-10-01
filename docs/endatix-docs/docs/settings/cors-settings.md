---
sidebar_position: 1
title: CORS Settings
---

# Configuring CORS Settings in Endatix

This guide will help you configure CORS (Cross-Origin Resource Sharing) settings for the Endatix application. By default if you omit the settings default CORS settings will be applied:

- On **Development** - very permissive CORS policy, allowing any origin, header and method, a.k.a. **AllowAll**
- On **Production** - very restrictive CORS policy, which disables all origins, headers and methods, a.k.a. **DisallowAll**

To override these settings and apply custom CORS policies, follow this guide.

:::note Note on importance

CORS is essential for managing how your web application interacts with resources from different origins. Properly configuring CORS ensures security while enabling the necessary cross-origin requests. Following the principle of least privilege is a good practice—expose only what is needed via CORS, and nothing more.

:::

# Getting it done

To get things up and running, paste the snippet below into your `appSettings.json` config file. Replace the values with what you need, and you're good to go!

```json
 "Endatix": {
    "Cors" : {
      "DefaultPolicyName": "ProductionCorsPolicy",
      "CorsPolicies": [
        {
          "PolicyName": "ProductionCorsPolicy",
          "AllowedOrigins": ["add.origins.here","example.look.right","https://my.domain"],
          "AllowedMethods": ["GET","POST","PATCH","DELETE","PUT","OPTIONS"],
          "AllowedHeaders": ["*"],
          "ExposedHeaders": [],
          "AllowCredentials": false,
          "PreflightMaxAgeInSeconds": 1200
        }
      ]
    }
  },
```

# Explanation

CORS functionality in Endatix is implemented via the ASP.NET Core [CORS middleware](https://learn.microsoft.com/en-us/aspnet/core/security/cors). All customization is done through unique settings managed in the appSettings.json config file.

## Root CORS Settings

These settings are defined under the `"Cors" : {...}` root section:

- **DefaultPolicyName**: This optional setting lets you specify the default CORS policy by name. If left empty, the system will fall back to the default policies (`AllowAll` in development and `DisallowAll` in non-development environments).
- **CorsPolicies**: Optional list of `CorsPolicySetting` configurations. Check the [CorsPolicySetting below](#corspolicysetting-settings) for info

## CorsPolicySetting settings

The CorsPolicySetting class defines individual CORS policies. You can create multiple policies, each with specific rules, and reference them in CorsSettings.

- **PolicyName**: A required unique identifier for each CORS policy. Use short and descriptive name
  - Examples: `"MainPolicy"` or `"StagingPolicy"`
- **AllowedOrigins**: A list of origins allowed to make cross-origin requests. Use "\*" to allow all origins or "-" to disallow all origins.
  - Example: `["https://my.domain.com", "https://my.domain.com"] - allows my.domain.com on both http and https as an origin
  - Example: `["*"]` - allows any origin. This is considered bad practice for production environments
  - Example: `["-"]` - disallows all origins. This disables CORS
  - Example: `["localhost:3000"]` - allows CORS requests originating from localhost on port 3000
- **AllowedMethods**: Specifies HTTP methods permitted for cross-origin requests. Use "\*" to allow all methods or "-" to disallow all methods. Values include the HTTP verbs like "GET", "POST", "PUT", "DELETE", "PATCH". More info [here](https://developer.mozilla.org/en-US/docs/Web/HTTP/Methods).
  - Example: `["GET","POST","PATCH","DELETE","PUT","OPTIONS"]` - these are all the HTTP methods used by the Endatix API
  - Example: `["*"] - allows all HTTP methods in CORS requests
- **AllowedHeaders**: Specifies which headers can be used in the request. Use "\*" to allow all headers or "-" to disallow all headers.
  - Example: `["Set-Cookie", "Accept"]` - adds "Set-Cookie" & "Accept" to the list of allowed headers when doing CORS requests
  - Example: `["*"]` - allows all headers in CORS requests. This is typically not recommended as it is too permissive.
- **ExposedHeaders**: Additional headers that can be exposed to the browser beyond the default simple response headers.
- **PreflightMaxAgeInSeconds**: Specifies how long the preflight request can be cached (in seconds). This is optional
- **AllowCredentials**: Determines if cross-origin credentials are allowed. By default, this is set to false to avoid security risks, especially when combined with `AllowAnyOrigin`.

:::tip Recommended Practices

- **Avoid Insecure Configurations**: Never combine `AllowAnyOrigin` with `AllowCredentials` as it can lead to security vulnerabilities like cross-site request forgery (CSRF).
- **Environment-Specific Policies**: Consider setting different policies for development and production to ensure both security and functionality.
  :::
