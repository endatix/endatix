# Endatix.Framework

This package provides common plugin and extensibility points used to extend or customize the Endatix Platform. It allows developers to build and integrate custom functionality within the platform.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET. It is designed for building secure, scalable, and integrated form-centric applications that work with SurveyJS. Endatix empowers business users with advanced workflows, automation, and meaningful insights.

## Installation:

```bash
dotnet add package Endatix.Framework
```

## Recommended Usage:

For running and hosting the Endatix Platform, **Endatix.Api.Host** is the recommended main package as it simplifies the installation and setup process.

```bash
dotnet add package Endatix.Api.Host
```

## Module kernel

Optional platform modules implement [`IEndatixModule`](Modules/IEndatixModule.cs). The host registers them via `EndatixBuilder.UseModule({Name}Module.Instance)`, which scans the module assembly for MediatR handlers and FastEndpoints and calls `ConfigureServices` at finalization.

| Contract | Purpose |
|----------|---------|
| `IEndatixModule` | Required entry point — `Assembly` + `ConfigureServices` |
| `IHasFeatureFlag` | Optional — module skipped when flag is disabled in `Endatix:FeatureFlags` |
| `IHasDbMigrations` | Optional marker — module ships EF migrations; host warns if no contributor was registered |
| `IDbContextMigrationContributor` | Opt-in startup migration contract for module/custom DbContexts |

### Startup migrations (two phases)

When `Endatix:Data:EnableAutoMigrations` is true, `DatabaseMigrationService` runs:

1. **Core (always)** — `AppDbContext` and `AppIdentityDbContext` are migrated automatically; no contributor registration required.
2. **Modules (opt-in)** — each module with its own DbContext calls `AddDbContextWithMigrations<TContext>` in `ConfigureServices` (registers DbContext + migration contributor).

### Module persistence checklist

1. Create `{Name}Module : IEndatixModule, IHasDbMigrations` (+ `IHasFeatureFlag` when optional).
2. In `ConfigureServices`, call `builder.AddDbContextWithMigrations<TContext>(...)` from `Endatix.Infrastructure.Data` (registers DbContext + migration contributor).
3. Use a dedicated schema and module-owned `Persistence/` folder for entities, configs, and migrations.
4. Prefer **provider-split DbContext types** (one snapshot per provider) over namespace-filtered single contexts when supporting PostgreSQL and SQL Server.
5. Do **not** add `Setup.cs` — all DI belongs on the module class.

## Observability (startup)

Startup logging uses **Microsoft.Extensions.Logging** source generation — not Serilog APIs — so hosts can switch to OpenTelemetry without changing call sites.

### Layer responsibilities

| Layer | Responsibility |
|-------|----------------|
| **Framework** | `EndatixEventIds` (global registry), `EndatixLoggerExtensions` (generic operation lifecycle only) |
| **Infrastructure / Hosting** | Domain-specific `*LoggerExtensions` collocated with the feature (e.g. `MigrationLoggerExtensions`, `EndatixBuilderLoggerExtensions`) |

Framework exposes only **generic** primitives:

- **`EndatixEventIds`** — platform-wide stable EventId registry (claim a range before adding domain extensions)
- **`EndatixLoggerExtensions`** — `LogOperationStarted`, `LogOperationCompleted`, `LogOperationSkipped`, `LogOperationFailed`

Domain-specific messages (migrations, seeding, host builders) belong in collocated `*LoggerExtensions` types in Infrastructure or Hosting — not in Framework.

### Event ID registry

`EndatixEventIds` follows the same grouping pattern as `Actions` — nested static classes per area, plus utility collections:

```csharp
EndatixEventIds.Lifecycle.OperationStarted      // Framework
EndatixEventIds.Migrations.DbContextMigrated    // Infrastructure
EndatixEventIds.Seeding.SampleDataSeeded        // Infrastructure
EndatixEventIds.IdentitySeed.UserCreated        // Infrastructure
EndatixEventIds.Hosting.ModuleRegistered        // Hosting

EndatixEventIds.Ranges.MigrationsStart          // claim before adding IDs
EndatixEventIds.Migrations.All                  // all IDs in a group
EndatixEventIds.IsMigration(eventId)            // range helpers
```

| Nested class | Range | Owner |
|--------------|-------|-------|
| `Lifecycle` | 1000–1003 | Framework |
| `Migrations` | 1004–1099 | Infrastructure |
| `Seeding` / `IdentitySeed` | 1100–1199 | Infrastructure |
| `Hosting` | 1200–1299 | Hosting |
| `Ranges.*` | 2000+ | Reserved (Auth, Forms, Webhooks) |

Claim a block in `EndatixEventIds.Ranges` before adding domain extensions.

### Usage

Always pass the caller's typed `ILogger` (e.g. `ILogger<DatabaseMigrationService>`) so logger category is preserved for tracing.

Use Framework primitives for lifecycle; domain wrappers for specialized messages:

```csharp
// Framework — generic lifecycle
_logger.LogOperationStarted(MigrationOperations.ApplyDbMigrations);

// Infrastructure — domain-specific (collocated)
_logger.LogDbContextMigrated(nameof(AppDbContext), durationMs);
```

Existing domain wrappers:

| Package | Location |
|---------|----------|
| Infrastructure | `Data/Logging/MigrationLoggerExtensions.cs`, `Data/Logging/DataSeedingLoggerExtensions.cs` |
| Infrastructure | `Identity/Seed/IdentitySeedLoggerExtensions.cs` |
| Hosting | `Builders/Logging/EndatixBuilderLoggerExtensions.cs` |

### OTEL attribute mapping

MEL placeholder → future log attribute:

| Placeholder | OTEL alignment |
|-------------|----------------|
| `Operation` | `operation.name` / custom dimension |
| `DbContext` | Application-specific; pairs with future `db.operation.name` spans |
| `DurationMs` | Duration in milliseconds |
| `DbSystem` | `db.system` (e.g. `SqlServer`, `PostgreSql`) |
| `Reason` | Skip/disable reason |
| `EventId` | Stable correlation via `EndatixEventIds` |

See [high-performance logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/high-performance-logging).

## More Information:
For detailed installation instructions, please visit [Endatix Installation Guide](https://docs.endatix.com/docs/getting-started/installation).