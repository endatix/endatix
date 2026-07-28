# Endatix.Modules.Reporting.Contracts

Extension-point contracts for Endatix reporting and submission export. This package contains only interfaces, DTOs and settings types — no implementation.

`Endatix.Api`'s submission export endpoint depends on these contracts and degrades gracefully when no implementation is registered, so a reporting implementation can be supplied by any package that implements them.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET. It is designed for building secure, scalable, and integrated form-centric applications that work with SurveyJS. Endatix empowers business users with advanced workflows, automation, and meaningful insights.

## Installation:

```bash
dotnet add package Endatix.Modules.Reporting.Contracts
```

You normally do not reference this package directly — it arrives as a transitive dependency of `Endatix.Api`. Reference it explicitly when implementing the contracts yourself.

## What's inside

- **Export contracts** — `IExportFormatRepository`, `IExportCapabilityRegistry`, `IColumnAliasTransformerRegistry`, `ExportTarget`, `ExportFormatSettings`, `ExportFilterContext`, `ExportRequestFilters`, `ColumnAliasProfile`
- **Submission integration** — `SubmissionIntegrationSnapshotDto`, `SubmissionIntegrationStatusCodes`

## License

See the [Endatix Platform repository](https://github.com/endatix/endatix) for license information.
