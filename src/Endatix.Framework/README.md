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

### Module persistence

Endatix has two persistence patterns. Every new module uses the **module pattern**.

| | Monolith pattern | Module pattern |
|---|---|---|
| Used by | `AppDbContext`, `AppIdentityDbContext` | Audience and Jobs. Reporting (one context, PostgreSQL migrations only today) and the SaaS modules are moving to it. |
| Context types | One context type for every provider | An abstract base context with the shared model, plus one sealed derived context per provider |
| Where migrations live | A separate assembly per provider: `Endatix.Persistence.PostgreSql`, `Endatix.Persistence.SqlServer` | The module assembly, under `Persistence/Migrations/<Provider>/` |
| Model snapshot | One per provider assembly, for the shared context type | One per derived context: `<Derived>ModelSnapshot.cs` |
| When to use | The core contexts only; do not add new ones | Every module that owns tables |

#### Module recipe

1. Create `{Name}Module : IEndatixModule, IHasDbMigrations` (+ `IHasFeatureFlag` when optional). All DI belongs on the module class; do **not** add `Setup.cs`.
2. **Base context.** An abstract `{Name}DbContextBase` owns the `DbSet`s, the module's own schema and the shared model. It applies the shared configurations explicitly (`modelBuilder.ApplyConfiguration(new XConfiguration())`), because an `[ApplyConfigurationFor<T>]` attribute can name only one context. It then calls an abstract `ApplyProviderConfigurations(modelBuilder)`, and finishes with `modelBuilder.ApplySnowflakeIdValueGenerators(Database)` and `modelBuilder.ApplyModuleTableNames()`.
3. **Derived contexts.** One sealed context per provider (`{Name}PostgreSqlDbContext`, `{Name}SqlServerDbContext`). Each overrides `ApplyProviderConfigurations` with `modelBuilder.ApplyConfigurationsFor<TDerived>(typeof(TDerived).Assembly)`.
4. **Provider configurations.** Column types, JSON columns and filtered-index SQL live in `Persistence/Config/<Provider>/`, each class marked `[ApplyConfigurationFor<TDerived>]`.
5. **Design-time factory.** One `IDesignTimeDbContextFactory<TDerived>` per derived context. It pins the provider (`UseNpgsql` or `UseSqlServer`) instead of reading `DefaultConnection_DbProvider`, sets the migrations assembly to the module assembly, and puts the migrations history table in the module schema. It reads `ConnectionStrings:DefaultConnection` (`ModuleDesignTimeConfiguration`) and fails when it is missing, so set a PostgreSQL connection string to scaffold PostgreSQL migrations and a SQL Server one to scaffold SQL Server migrations. `migrations add` does not connect to the database.
6. **Runtime registration.** In `ConfigureServices`, pick the derived context with `DatabaseProviderResolver.IsPostgreSql(configuration)`, register it with `builder.AddDbContextWithMigrations<TDerived>(...)` from `Endatix.Infrastructure.Data`, and map the module interface to it: `builder.Services.AddScoped<I{Name}DbContext>(sp => sp.GetRequiredService<TDerived>())`. Consumers depend on the interface only, so nothing downstream branches on the provider.
7. **Migrations namespaces.** Set both `PostgreSqlMigrationsNamespace` and `SqlServerMigrationsNamespace` to the module's migrations root namespace (`Endatix.Modules.<Name>.Persistence.Migrations`), never to a provider folder. Registration requires the active provider's namespace, and two equal values keep namespace filtering off: each derived context already finds its own migrations by their `[DbContext]` attribute.

#### Adding a migration

Run from the repository root, with `Endatix.WebHost` as the startup project.

```bash
# PostgreSQL
ConnectionStrings__DefaultConnection="Host=localhost;Database=endatix;Username=postgres;Password=..." \
dotnet ef migrations add <MigrationName> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.<Name> \
  --context <Name>PostgreSqlDbContext \
  --output-dir Persistence/Migrations/PostgreSql

# SQL Server
ConnectionStrings__DefaultConnection="Server=localhost;Database=endatix;User Id=sa;Password=...;TrustServerCertificate=True" \
dotnet ef migrations add <MigrationName> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.<Name> \
  --context <Name>SqlServerDbContext \
  --output-dir Persistence/Migrations/SqlServer
```

Module SQL Server migrations need **SQL Server 2025 or later, or Azure SQL Database**.

#### Why not one context with two migration folders

One context type cannot own both providers' migrations in one assembly, because of two EF Core rules:

- **Discovery.** EF finds a context's migrations by matching `Context.GetType()` against each migration's `[DbContext(typeof(...))]` attribute. With one context type, both folders match, and each provider would see the other's migrations.
- **Snapshot placement.** EF reads and writes the snapshot as `{ContextName}ModelSnapshot.cs`, one per context type. Two providers would overwrite one snapshot.

The monolith pattern avoids both by putting each provider's migrations in its own assembly.

#### Applied migrations: retarget, never regenerate

When an existing context moves to this pattern, change only the `[DbContext(typeof(...))]` attribute in its `.Designer.cs` files and its snapshot to the derived type. The migration id, `Up()` and `Down()` stay byte-identical. A regenerated migration gets a new id, and every database that applied the old one would run it again. Deleting the other provider's `ValueGenerationStrategy` annotation from a designer is also safe: the designer's target model reaches only the active provider's SQL generator, which ignores the other provider's annotations, so the migration's SQL does not change.

Check a snapshot with a throwaway migration, then delete it:

```bash
dotnet ef migrations add Probe --context <Derived> --output-dir Persistence/Migrations/<Provider> \
  --startup-project src/Endatix.WebHost --project src/Endatix.Modules.<Name>
```

The check passes when the generated `Up()` and `Down()` are empty and `git status --porcelain` lists only the two new `*_Probe*.cs` files, so the committed snapshot is unchanged. Delete both files; never commit them.

#### Id annotations in snapshots

`ApplySnowflakeIdValueGenerators(Database)` writes only the active provider's `ValueGenerationStrategy` annotation on each `long Id` key, so `migrations add` scaffolds snapshots and designers that need no hand edits. The parameterless `ApplySnowflakeIdValueGenerators()` writes both annotations, so every scaffolded snapshot and designer also carries the other provider's annotation. That line names the other provider's EF types, so it compiles only while the project references both EF providers. Deleting it by hand is easy to get wrong: removing the wrong line of the chained annotation drops the provider's own annotation without any error. The parameterless overload is obsolete and will be removed after the next stable release.

Every committed snapshot and designer in this repository carries only its own provider's annotation. `CommittedModelProviderAnnotationsTests` in `Endatix.Infrastructure.Tests` fails CI when one does not; `has-pending-model-changes` cannot catch it, because it compares relational models only.

Model snapshots never contain the `ValueGeneratorFactory` annotation: EF Core filters it out of every snapshot by design and does not compare it. Do not restore it by hand.

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