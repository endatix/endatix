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

## More Information:
For detailed installation instructions, please visit [Endatix Installation Guide](https://docs.endatix.com/docs/getting-started/installation).