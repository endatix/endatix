# Endatix.Modules.Reporting

Reporting bounded context for BI-ready survey exports.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET. It is designed for building secure, scalable, and integrated form-centric applications that work with SurveyJS. Endatix empowers business users with advanced workflows, automation, and meaningful insights.

## Installation:

```bash
dotnet add package Endatix.Modules.Reporting
```

## Recommended Usage:

For running and hosting the Endatix Platform, **Endatix.Api.Host** is the recommended main package as it simplifies the installation and setup process.

```bash
dotnet add package Endatix.Api.Host
```

The module is registered automatically by `EndatixBuilder.UseDefaults()` — no host code change is needed to get it, and none is needed to keep it off. Availability is decided at runtime by the feature flag (see [Registration](#registration)), not by whether the package is present.

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
| `ExportFormats` | Tenant export rows (CSV, JSON, Excel/XLSX, codebook) |
| `SurveyTypeExportMappings` | Allowed export formats per survey type (with optional default and tenant fallback) |

`FlattenedSubmissions.SubmissionId` is the row key. Backfill and the completion outbox both update that row; they do not insert a second one.

`POST …/reporting/submissions/backfill` pages one scope per call. Omitted `completionScope` is `completed`. `incomplete` flattens drafts. The outbox still skips drafts. A processed row is skipped when the submission's `ModifiedAt ?? CreatedAt` is not newer than the row's `SourceModifiedAt` (the stamp of the version that was flattened), unless `force` is true. The row's own `ModifiedAt` is not used: the worker sets it after reading the submission, so a save during flattening would look older and never be refreshed. Rows without `SourceModifiedAt` (processed before it existed) are reprocessed once. A flatten whose submission is gone marks an existing row deleted only when the submission was soft-deleted in the same tenant and form; otherwise it throws, so the outbox retries and backfill reports it as failed. With background jobs on (`Endatix:Outbox:DeliverToJobQueue`), the flatten job retries instead and then dead-letters. A submission deleted after the flatten read it loses the row the flatten wrote, hard-deleted as its deletion sync removes it. Export drops soft-deleted submissions.

Flattens of one submission can run out of order, as retries or as jobs, so each write is guarded by `SourceRevision`: the submission's `Revision` the row was written from. A write lands only when the row has no `SourceRevision` or one not newer than the write's, so an older flatten finishing last changes nothing, and a forced rebuild at the same revision still lands. `SourceRevision` orders writes; `SourceModifiedAt` only tells backfill whether a row is stale. A draft's saves raise no events and so share one revision: among them, the last flatten to write wins.

### FormSchema compile modes

On every compile path (outbox `form.definition.updated`, manual `POST .../reporting/compile-schema`):

| Mode | When | Behavior |
|------|------|----------|
| **Replace** | Form has **0 real** submissions (`IsTestSubmission == false`) | Rebuild FlatteningMap + Codebook from the current definition only. After save, hard-delete that form’s `FlattenedSubmissions` rows (test flatten debris). |
| **Merge** | Form has **≥1 real** submission | Append-only merge: retain historical columns, questions, and choice-catalog values. |

Test submissions alone do **not** force merge. This is a defensive bridge until Form Publish makes publish the controlled compile moment.

## Registration

Registered via `EndatixBuilder.UseDefaults()` → `UseModule(ReportingModule.Instance)`. Capabilities: `IEndatixModule`, `IHasFeatureFlag`, `IHasDbMigrations`, `IHasFastEndpoints` (serializers/OpenAPI tags via `ConfigureFastEndpoints`). Do **not** also `Api.ScanAssemblies` this assembly. `ReportingPersistence.ConfigureDbContextOptions` sets both namespaces, but **runtime requires PostgreSQL** until [#813](https://github.com/endatix/endatix/issues/813) (no SQL Server migrations; auto-migrate does not create `reporting`). Disabled by default until:

```json
"Endatix": {
  "FeatureFlags": {
    "ReportingModule": true
  }
}
```

## Migrations

Run the commands from the `oss` folder.

> [!NOTE]
> Always use `Endatix.WebHost` as startup project, and this module as the migrations project (`ReportingDbContext`).
> Set `ConnectionStrings:DefaultConnection_DbProvider` to the provider you are generating for.

Migrations live in provider-specific subfolders under `Persistence/Migrations/`:

- `Persistence/Migrations/PostgreSql/` — **available** (`InitialReporting`, `SeedDefaultExportFormats`, `AddFlattenedSubmissionSourceModifiedAt`, `AddFlattenedSubmissionSourceRevision`)

  Tenant export formats are rows, not code. Runtime catalog: `DefaultExportFormats.All`
  (`SeedDefaultsAsync` / `tenant.created`). **Existing tenants:** frozen SQL in
  `SeedDefaultExportFormats` — do not generate that migration from the catalog; a new default
  needs a new data migration. **New tenants:** outbox `tenant.created` → `IDefaultExportFormatsSeeder`.
- `Persistence/Migrations/SqlServer/` — **not yet available**. SQL Server support is coming soon
  ([endatix/endatix#813](https://github.com/endatix/endatix/issues/813)). Use PostgreSQL for Reporting until then.

> **SQL Server hosts:** Do not enable `ReportingModule` with `Endatix:Data:EnableAutoMigrations` until SQL Server
> migrations land in #813. The module DbContext and migration contributor register on SQL Server, but startup
> auto-migration finds no migrations for the active provider and logs an error (the `reporting` schema is not created).

Startup also applies Reporting migrations when `Customizations:Reporting:ApplyMigrationsAtStartup` is true (default)
and `Endatix:Data:EnableAutoMigrations` is enabled.

### Add migration

PostgreSQL only (SQL Server: wait for #813).

```bash
dotnet ef migrations add <MigrationName> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingDbContext \
  --output-dir Persistence/Migrations/PostgreSql
```

### Remove last migration

1. List migrations and check whether the last one is already applied locally:

```bash
dotnet ef migrations list \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingDbContext
```

2. If it is applied, roll the database back to the previous migration:

```bash
dotnet ef database update <PreviousMigrationName> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingDbContext
```

3. Remove the last migration:

```bash
dotnet ef migrations remove \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingDbContext
```

### Apply migrations

```bash
dotnet ef database update \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingDbContext
```
