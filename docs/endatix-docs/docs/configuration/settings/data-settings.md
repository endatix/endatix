---
sidebar_position: 1
title: Data Settings
---

# Configuring Data Settings in Endatix

Data settings control the behavior of the data persistence layer, including database migrations and sample data seeding.

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

When `SeedSampleData` is enabled, you can configure the initial user with these properties:

| Property | Description | Required |
|----------|-------------|----------|
| `Email` | Email address for the initial user | Yes |
| `Password` | Password for the initial user | Yes |

## Programmatic Configuration

You can also configure data settings using the builder pattern:

```csharp
builder.Services.AddEndatix(configuration)
    .Persistence
        .EnableAutoMigrations(true)
        .EnableSampleDataSeeding(true)
    .Build();
```

:::warning Note on importance
 
:fire: Data settings are a critical part of your deployment strategy. You should carefully decide when and if data migrations and seeding are executed to ensure your database schema and data are properly managed according to your application's needs.

:::