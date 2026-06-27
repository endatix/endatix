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
| `FormExportSchemas` | Append-only flattened column spec per tenant + form |
| `FlattenedSubmissions` | Flat submission answers aligned to form schema |
| `ExportFormats` | Export delivery configuration (CSV, JSON, codebook) |
| `SurveyTypeExportMappings` | Allowed export formats per survey type (with optional default and tenant fallback) |

## Registration

The Reporting module is registered by `EndatixBuilder.UseDefaults()` and implements `IHasFeatureFlag`. It is **disabled by default** until enabled in configuration:

```json
"Endatix": {
  "FeatureFlags": {
    "ReportingModule": true
  }
}
```

## Migrations

Migrations live in provider-specific subfolders under `Persistence/Migrations/`:

- `Persistence/Migrations/PostgreSql/`
- `Persistence/Migrations/SqlServer/`

Set `ConnectionStrings:DefaultConnection_DbProvider` to match the provider you are generating for.

### PostgreSQL

```bash
dotnet ef migrations add <Name> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingDbContext \
  --output-dir Persistence/Migrations/PostgreSql
```

### SQL Server

```bash
ConnectionStrings__DefaultConnection_DbProvider=SqlServer \
dotnet ef migrations add <Name> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Modules.Reporting \
  --context ReportingDbContext \
  --output-dir Persistence/Migrations/SqlServer
```

Migrations apply automatically at startup when `Customizations:Reporting:ApplyMigrationsAtStartup` is true (default) and `Endatix:Data:EnableAutoMigrations` is enabled.
