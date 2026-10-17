---
sidebar_position: 2
title: Persistence Settings
---

# Persistence Settings

These are **Endatix API** settings (`Endatix:Persistence`). They pick the database provider, connection strings, and migration behavior.

## Configuring the Database Provider

The easiest way to specify which database provider Endatix should use is through the configuration:

```json
"Endatix": {
  "Persistence": {
    "Provider": "PostgreSql"  // Or "SqlServer"
  }
}
```

Supported providers are **SQL Server** and **PostgreSQL**. On PostgreSQL, the `pg_trgm` extension is required for data-list label search — see [System Requirements](/docs/getting-started/system-requirements#postgresql-pg_trgm-extension).

SQL Server must be **SQL Server 2025 or later, or Azure SQL Database**: the schema uses SQL Server's native `json` type.

The Reporting module (`Endatix:FeatureFlags:ReportingModule`) runs on both providers and migrates its `reporting` schema at startup on either.

The Background Jobs module (`Endatix:FeatureFlags:JobsModule`) runs on both providers too, and migrates its `jobs` schema, including the job scheduler's tables, at startup on either. On SQL Server, enqueueing a job holds the scheduler's cluster-wide trigger lock until its transaction commits (`Endatix:BackgroundJobs:LockOnInsert`, `true` by default on SQL Server). Setting it to `false` requires `READ_COMMITTED_SNAPSHOT` on for the database; a node configured that way refuses to start without it. The [Jobs module README](https://github.com/endatix/endatix/blob/main/src/Endatix.Modules.Jobs/README.md#trigger-lock-on-sql-server) explains when that trade is worth making and gives the measurements behind it.

### Audience on SQL Server

The Audience module (`Endatix:FeatureFlags:PersonalizationModule`) runs on both providers.

Keep `READ_COMMITTED_SNAPSHOT` on for the database. A database that Endatix creates on first start has it on, and so does Azure SQL Database; a database created beforehand (by a DBA or a script) may have it off. Without it, a read waits for any writer whose uncommitted rows it scans: while one tenant imports a 5,000-row CSV, another tenant's audience requests can wait 1.5–2 seconds for the import to commit. With it on, their reads do not wait; a write can still wait briefly near the end of a large import, when SQL Server escalates the import's row locks to table locks. Check and turn it on with:

```sql
SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME();
ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
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

:::warning[Note]
Database connection strings should be stored securely using secrets management in production environments.
::: 