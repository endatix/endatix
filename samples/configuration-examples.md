# Authentication Provider Configuration Examples

This document provides examples of how to configure authentication providers in Endatix using the new provider system.

## Basic Configuration

### Default Setup (Auto-Discovery)
When no explicit configuration is provided, Endatix will automatically register built-in providers:

```json
{
  "Endatix": {
    "Authentication": {
      "EnableAutoDiscovery": true
    }
  }
}
```

This will register:
- Endatix JWT provider (priority 0)
- Keycloak provider (priority 10, development defaults)

### Minimal Explicit Configuration

```json
{
  "Endatix": {
    "Authentication": {
      "Providers": [
        {
          "Id": "endatix",
          "Type": "jwt",
          "Enabled": true
        }
      ]
    }
  }
}
```

## Multi-Provider Configurations

### Endatix + Keycloak Production Setup

```json
{
  "Endatix": {
    "Authentication": {
      "DefaultScheme": "Endatix",
      "Providers": [
        {
          "Id": "endatix",
          "Type": "jwt",
          "Enabled": true,
          "Priority": 0,
          "Config": {
            "ValidateIssuer": true,
            "ValidateAudience": true,
            "ValidateLifetime": true,
            "RequireHttpsMetadata": true
          }
        },
        {
          "Id": "keycloak-prod",
          "Type": "keycloak",
          "Enabled": true,
          "Priority": 10,
          "Config": {
            "MetadataAddress": "https://auth.example.com/realms/endatix/.well-known/openid-configuration",
            "ValidIssuer": "https://auth.example.com/realms/endatix",
            "Audience": "endatix-client",
            "RequireHttpsMetadata": true,
            "ValidateIssuer": true,
            "ValidateAudience": true,
            "ValidateLifetime": true,
            "ValidateIssuerSigningKey": true,
            "MapInboundClaims": true
          }
        }
      ]
    }
  }
}
```

### Development Setup with Multiple Keycloak Realms

```json
{
  "Endatix": {
    "Authentication": {
      "Providers": [
        {
          "Id": "endatix",
          "Type": "jwt",
          "Enabled": true,
          "Priority": 0
        },
        {
          "Id": "keycloak-main",
          "Type": "keycloak",
          "Enabled": true,
          "Priority": 10,
          "Config": {
            "MetadataAddress": "http://localhost:8080/realms/endatix/.well-known/openid-configuration",
            "ValidIssuer": "http://localhost:8080/realms/endatix",
            "Audience": "account",
            "RequireHttpsMetadata": false,
            "ValidateIssuer": false,
            "ValidateAudience": false,
            "ValidateLifetime": false,
            "ValidateIssuerSigningKey": false
          }
        },
        {
          "Id": "keycloak-test",
          "Type": "keycloak",
          "Enabled": true,
          "Priority": 11,
          "Config": {
            "MetadataAddress": "http://localhost:8080/realms/test/.well-known/openid-configuration",
            "ValidIssuer": "http://localhost:8080/realms/test",
            "Audience": "test-client",
            "RequireHttpsMetadata": false,
            "ValidateIssuer": false,
            "ValidateAudience": false,
            "ValidateLifetime": false,
            "ValidateIssuerSigningKey": false
          }
        }
      ]
    }
  }
}
```

## Programmatic Configuration

### Using Fluent API

```csharp
// In Program.cs
builder.Host.UseEndatix(endatix => endatix
    .WithSecurity(security => security
        .UseConfiguredProviders()  // Use configuration-driven setup
        .AddKeycloak(options =>    // Add additional provider programmatically
        {
            options.MetadataAddress = "https://auth.example.com/realms/custom/.well-known/openid-configuration";
            options.ValidIssuer = "https://auth.example.com/realms/custom";
            options.Audience = "my-app";
            options.RequireHttpsMetadata = true;
        })
        .ConfigureProviders(authOptions =>  // Override configuration programmatically
        {
            authOptions.DefaultScheme = "Keycloak";
        })));
```

### Legacy Mode (Backward Compatibility)

```csharp
// Still supported for existing applications
builder.Host.UseEndatix(endatix => endatix
    .WithSecurity(security => security
        .UseJwtAuthentication(options =>
        {
            options.TokenValidationParameters.ValidateIssuer = false;
        })));
```

## Advanced Scenarios

### Custom Provider Priority Override

```json
{
  "Endatix": {
    "Authentication": {
      "Providers": [
        {
          "Id": "keycloak-primary",
          "Type": "keycloak",
          "Enabled": true,
          "Priority": 0,
          "Config": {
            "MetadataAddress": "https://primary-auth.example.com/realms/main/.well-known/openid-configuration"
          }
        },
        {
          "Id": "endatix",
          "Type": "jwt",
          "Enabled": true,
          "Priority": 10
        }
      ]
    }
  }
}
```

### Conditional Provider Enabling

```json
{
  "Endatix": {
    "Authentication": {
      "Providers": [
        {
          "Id": "endatix",
          "Type": "jwt",
          "Enabled": true,
          "Priority": 0
        },
        {
          "Id": "keycloak-dev",
          "Type": "keycloak",
          "Enabled": false,
          "Priority": 10,
          "Config": {
            "MetadataAddress": "http://localhost:8080/realms/endatix/.well-known/openid-configuration"
          }
        }
      ]
    }
  }
}
```

## Configuration Properties Reference

### Root Authentication Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableAutoDiscovery` | boolean | `true` | Automatically register built-in providers when no explicit configuration |
| `DefaultScheme` | string | `"Endatix"` | Default authentication scheme when no provider matches |
| `Providers` | array | `[]` | Array of provider configurations |

### Provider Options

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Id` | string | Yes | Unique identifier for the provider |
| `Type` | string | Yes | Provider type (`"jwt"`, `"keycloak"`) |
| `Enabled` | boolean | No (default: `true`) | Whether the provider is enabled |
| `Priority` | integer | No | Provider priority (lower = higher priority) |
| `Config` | object | No | Provider-specific configuration |

### Keycloak Provider Config

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MetadataAddress` | string | Required | OpenID Connect metadata endpoint |
| `ValidIssuer` | string | Optional | Expected token issuer |
| `Audience` | string | `"account"` | Expected token audience |
| `RequireHttpsMetadata` | boolean | `true` | Require HTTPS for metadata |
| `ValidateIssuer` | boolean | `true` | Validate token issuer |
| `ValidateAudience` | boolean | `true` | Validate token audience |
| `ValidateLifetime` | boolean | `true` | Validate token lifetime |
| `ValidateIssuerSigningKey` | boolean | `true` | Validate signing key |
| `MapInboundClaims` | boolean | `true` | Map Keycloak claims to standard format |

### Endatix JWT Provider Config

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ValidateIssuer` | boolean | `true` | Validate token issuer |
| `ValidateAudience` | boolean | `true` | Validate token audience |
| `ValidateLifetime` | boolean | `true` | Validate token lifetime |
| `ValidateIssuerSigningKey` | boolean | `true` | Validate signing key |
| `RequireHttpsMetadata` | boolean | `!isDevelopment` | Require HTTPS metadata |
| `ClockSkewSeconds` | integer | `15` | Clock skew tolerance in seconds |

## Migration Guide

### From Hardcoded to Provider System

**Before (hardcoded):**
```csharp
builder.Host.UseEndatix(endatix => endatix
    .WithSecurity(security => security
        .UseJwtAuthentication()));
```

**After (provider system):**
```csharp
builder.Host.UseEndatix(endatix => endatix
    .WithSecurity(security => security
        .UseConfiguredProviders()));
```

Add this to your `appsettings.json`:
```json
{
  "Endatix": {
    "Authentication": {
      "EnableAutoDiscovery": true
    }
  }
}
```

### Customizing Default Providers

If you need to customize the default behavior, disable auto-discovery and configure explicitly:

```json
{
  "Endatix": {
    "Authentication": {
      "EnableAutoDiscovery": false,
      "Providers": [
        {
          "Id": "endatix",
          "Type": "jwt",
          "Enabled": true,
          "Config": {
            "ValidateIssuer": false,
            "ValidateAudience": false
          }
        }
      ]
    }
  }
}
```

## Troubleshooting

### Common Issues

1. **No providers registered**: Ensure `EnableAutoDiscovery` is `true` or configure providers explicitly
2. **Wrong scheme selected**: Check provider priorities and issuer patterns
3. **Configuration not found**: Verify section path is `"Endatix:Authentication"`
4. **Validation errors**: Check required fields like `MetadataAddress` for Keycloak

### Debug Logging

Enable debug logging to see provider registration and scheme selection:

```json
{
  "Logging": {
    "LogLevel": {
      "Endatix.Hosting.Builders.EndatixSecurityBuilder": "Debug",
      "Endatix.Infrastructure.Identity.Authentication": "Debug"
    }
  }
}
``` 