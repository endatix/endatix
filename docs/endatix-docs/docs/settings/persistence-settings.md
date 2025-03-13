---
sidebar_position: 2
title: Persistence Settings
---

# Configuring Persistence Settings in Endatix

Persistence settings control how Endatix interacts with your database, including which database provider to use, connection strings, and migration behavior.

## Configuring the Database Provider

The easiest way to specify which database provider Endatix should use is through the configuration:

```json
"Endatix": {
  "Persistence": {
    "Provider": "PostgreSql"  // Or "SqlServer"
  }
}
```

## Usage Examples

### 1. Automatic Configuration with Defaults

The simplest way to configure persistence with defaults:

```csharp
// Automatically selects the provider from configuration
builder.Host.ConfigureEndatix();
```

### 2. Direct Provider Selection with Full Custom Configuration

Complete control over persistence configuration:

```csharp
// Full custom configuration
builder.Host.ConfigureEndatix(endatix => {
    endatix.WithPersistence(persistence => persistence
        .UseSqlServer<AppDbContext>(options => {
            options.ConnectionString = "Server=myserver;Database=mydb;Trusted_Connection=True;";
            options.EnableSensitiveDataLogging = true;
            options.CommandTimeout = 60;
            options.MaxRetryCount = 5;
        })
        .EnableAutoMigrations()
        .EnableSampleDataSeeding());
});
```

### 3. Hybrid Approach - Defaults Plus Customization

Start with defaults and customize only what you need:

```csharp
// Hybrid approach
builder.Host.ConfigureEndatixWithDefaults(endatix => {
    endatix.WithPersistence(persistence => {
        // Override the connection string
        persistence.UseSqlServer<AppDbContext>(options => {
            options.ConnectionString = "your_custom_connection_string";
        });
        
        // Enable auto migrations
        persistence.EnableAutoMigrations();
    });
});
```

#### Convenience Methods

For scenarios where you only need to configure the database context without other customizations:

```csharp
// Use the convenience method for SQL Server
builder.Host.ConfigureEndatix(endatix => 
    endatix.UseSqlServer<AppDbContext>());

// Use the convenience method for PostgreSQL
builder.Host.ConfigureEndatix(endatix => 
    endatix.UsePostgreSql<AppDbContext>());
```

## Available Settings

| Setting | Description | Default Value |
|---------|-------------|--------------|
| `Endatix:Persistence:Provider` | The database provider to use (`SqlServer` or `PostgreSql`) | `SqlServer` |
| `ConnectionStrings:DefaultConnection` | The default database connection string | *None* |
| `ConnectionStrings:DbProvider` | The database provider to use (`SqlServer` or `PostgreSql`) | *SqlServer* |

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
    });
```

## Combining with Other Settings

You can combine persistence configuration with other Endatix features using the builder pattern:

```csharp
services.AddEndatix(configuration)
    .UseSqlServer<AppDbContext>()
    .EnableAutoMigrations()
    .EnableSampleDataSeeding()
    .Api.AddSwagger().Build()
    .Security.UseJwtAuthentication().Build();
```

:::warning Note
Database connection strings should be stored securely using secrets management in production environments.
::: 