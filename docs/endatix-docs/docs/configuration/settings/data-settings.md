---
sidebar_position: 1
title: Data Settings
---

# Data Settings

These are **Endatix API** settings (`Endatix:Data`). They control migrations and sample seeding.

## Configuration

To configure the data settings, add the following snippet to your `appsettings.json` file. Customize the values based on your requirements:

```json
"Endatix": {
    "Data": {
        "EnableAutoMigrations": true,
        "SeedSampleData": true,
        "InitialUser": {
            "Email": "admin@example.com",
            "Password": "StrongPassword123!"
        }
    }
}
```

## Settings Reference

| Setting | Description | Default |
|---------|-------------|---------|
| `EnableAutoMigrations` | Controls whether database migrations are automatically applied at application startup | `false` |
| `SeedSampleData` | Controls whether initial sample data (including user) is seeded at startup | `false` |
| `InitialUser` | Configuration for the initial admin user when seeding sample data | *(see below)* |

### InitialUser Options

When `SeedSampleData` is enabled and the database has no users, the API creates the initial admin from these properties:

| Property | Description | Required |
|----------|-------------|----------|
| `Email` | Email address for the initial user. Defaults to `admin@endatix.com` | No |
| `Password` | Password for the initial user. There is no default | Yes |

Without `Password`, the API starts but creates no admin. It logs this error (event id `1110`):

```text
❌ The initial admin user was not created because Endatix:Data:InitialUser:Password is not configured.
```

Set the password and restart. Outside your machine, pass it as the `Endatix__Data__InitialUser__Password` environment variable or a secret, not in a committed `appsettings.json`. After the first sign-in, change the password and remove `InitialUser` from the configuration.

:::info[Changed in 0.8.1]
Earlier versions fell back to `admin@endatix.com` / `P@ssw0rd` when `InitialUser` was missing, and the API's `appsettings.json` shipped that password. Both are gone. Only `appsettings.Development.json` still carries a development password.
:::

## Programmatic Configuration

You can also configure data settings using the builder pattern:

```csharp
builder.Services.AddEndatix(configuration)
    .Persistence
        .EnableAutoMigrations(true)
        .EnableSampleDataSeeding(true)
    .Build();
```

:::warning[Note on importance]
 
:fire: Data settings are a critical part of your deployment strategy. You should carefully decide when and if data migrations and seeding are executed to ensure your database schema and data are properly managed according to your application's needs.

:::