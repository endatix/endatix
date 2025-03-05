---
sidebar_position: 2
title: Persistence Settings
---

# Configuring Persistence Settings in Endatix

Persistence settings control how Endatix interacts with your database, including which database provider to use, connection strings, and migration behavior.

## Configuring the Database Provider

The easiest way to specify which database Endatix should use is through the configuration:

```json
"Endatix": {
  "Persistence": {
    "Provider": "PostgreSql"  // Or "SqlServer"
  }
}
```

## Usage Examples

### Automatic Configuration

When using `AddEndatix().UseDefaults()`, the database provider will be automatically selected based on your configuration. If not specified, SQL Server is used by default:

```csharp
// Uses the provider from configuration, or SQL Server if not specified
services.AddEndatix(configuration)
    .UseDefaults();
```

### Direct Provider Selection

You can specify the database provider directly in code using the new convenience methods:

```csharp
// Option 1: Using convenience method with SQL Server
services.AddEndatixWithSqlServer<AppDbContext>(configuration);

// Option 2: Using convenience method with PostgreSQL
services.AddEndatixWithPostgreSql<AppDbContext>(configuration);

// Option 3: Using builder methods directly
services.AddEndatix(configuration)
    .UseSqlServer<AppDbContext>();

// Option 4: With custom options
services.AddEndatix(configuration)
    .UsePostgreSql<AppDbContext>(options => {
        options.ConnectionString = "your_connection_string";
        options.EnableSensitiveDataLogging = true;
    });
```

> **Note**: All persistence configuration methods automatically register both `AppDbContext` and `AppIdentityDbContext` with the same database provider and settings. You only need to specify one of them.

### Legacy Method (Requires WithPersistence)

For backward compatibility, you can still use the persistence builder explicitly:

```csharp
services.AddEndatix(configuration)
    .WithPersistence()
    .UsePostgreSql<AppDbContext>();
```

## Available Settings

| Setting | Description | Default Value |
|---------|-------------|--------------|
| `Endatix:Persistence:Provider` | The database provider to use (`SqlServer` or `PostgreSql`) | `SqlServer` |

## Provider-Specific Options

Each database provider supports additional configuration options:

### SQL Server Options

```csharp
services.AddEndatix(configuration)
    .UseSqlServer<AppDbContext>(options => {
        options.ConnectionString = "your_connection_string";
        options.MigrationsAssembly = "Your.Migrations.Assembly";
        options.CommandTimeout = 30;
        options.MaxRetryCount = 5;
        options.MaxRetryDelay = 30;
        options.EnableSensitiveDataLogging = false;
        options.EnableDetailedErrors = false;
        options.AutoMigrateDatabase = false;
    });
```

### PostgreSQL Options

```csharp
services.AddEndatix(configuration)
    .UsePostgreSql<AppDbContext>(options => {
        options.ConnectionString = "your_connection_string";
        options.MigrationsAssembly = "Your.Migrations.Assembly";
        options.CommandTimeout = 30;
        options.MaxRetryCount = 5;
        options.MaxRetryDelay = 30;
        options.EnableSensitiveDataLogging = false;
        options.EnableDetailedErrors = false;
        options.AutoMigrateDatabase = false;
    });
```

:::warning Note
Database connection strings should be stored securely using secrets management in production environments.
::: 