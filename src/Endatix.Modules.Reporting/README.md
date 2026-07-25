# Endatix.Modules.Reporting

Reporting bounded context for BI-ready survey exports.

## Module layout (Modulith)

| Package | Contents |
|---------|----------|
| `Endatix.Modules.Reporting.Contracts` | Public API surface: status codes, read DTOs, future commands/queries/events |
| `Endatix.Modules.Reporting` | Domain (`SubmissionIntegrationState`, `FlattenedSubmission`, …), persistence, features |

Integration pipeline state lives on `FlattenedSubmission` in the `reporting` schema. A future reporting read API can expose `SubmissionIntegrationSnapshotDto` per submission without touching core `Submission` list endpoints.

## Schema

Database schema: `reporting`

| Table | Purpose |
|-------|---------|
| `FormSchemas` | Compiled form schema (`FlatteningMap` + `Codebook`) per tenant + form |
| `FlattenedSubmissions` | Flat submission answers aligned to form schema |
| `ExportFormats` | Export delivery configuration (CSV, JSON, codebook) |
| `SurveyTypeExportMappings` | Allowed export formats per survey type (with optional default and tenant fallback) |

### FormSchema compile modes

On every compile path (outbox `form.definition.updated`, manual `POST .../reporting/compile-schema`, lazy provider compile):

| Mode | When | Behavior |
|------|------|----------|
| **Replace** | Form has **0 real** submissions (`IsTestSubmission == false`) | Rebuild FlatteningMap + Codebook from the current definition only. After save, hard-delete that form’s `FlattenedSubmissions` rows (test flatten debris). |
| **Merge** | Form has **≥1 real** submission | Append-only merge: retain historical columns, questions, and choice-catalog values. |

Test submissions alone do **not** force merge. This is a defensive bridge until Form Publish makes publish the controlled compile moment.

## Registration

Registered via `EndatixBuilder.UseDefaults()` → `UseModule(ReportingModule.Instance)`. The module class implements `IEndatixModule` and `IHasFeatureFlag`. It is **disabled by default** until enabled in configuration:

```json
"Endatix": {
  "FeatureFlags": {
    "ReportingModule": true
  }
}
```

## Migrations

Migrations live in provider-specific subfolders under `Persistence/Migrations/`:

- `Persistence/Migrations/PostgreSql/` — **available** (`InitialReporting`)
- `Persistence/Migrations/SqlServer/` — **not yet available** ([endatix/endatix#813](https://github.com/endatix/endatix/issues/813))

Set `ConnectionStrings:DefaultConnection_DbProvider` to match the provider you are generating for.

> **SQL Server hosts:** Do not enable `ReportingModule` with `Endatix:Data:EnableAutoMigrations` until SQL Server migrations land in #813. The module DbContext and migration contributor register on SQL Server, but startup auto-migration finds no migrations for the active provider and logs an error (the `reporting` schema is not created).

### PostgreSQL

```bash
dotnet ef migrations add <Name> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingDbContext \
  --output-dir Persistence/Migrations/PostgreSql
```

### SQL Server

> Blocked until [#813](https://github.com/endatix/endatix/issues/813). Use PostgreSQL for Reporting until SQL Server migrations are added.

```bash
ConnectionStrings__DefaultConnection_DbProvider=SqlServer \
dotnet ef migrations add <Name> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingDbContext \
  --output-dir Persistence/Migrations/SqlServer
```

Migrations apply automatically at startup when `Customizations:Reporting:ApplyMigrationsAtStartup` is true (default) and `Endatix:Data:EnableAutoMigrations` is enabled.
