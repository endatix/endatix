# Endatix.Modules.Reporting.Contracts

The public API surface of the Endatix Platform Reporting module: export contracts, read DTOs and status codes. This package contains only interfaces, DTOs and settings types — no implementation.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET. It is designed for building secure, scalable, and integrated form-centric applications that work with SurveyJS. Endatix empowers business users with advanced workflows, automation, and meaningful insights.

## Installation:

```bash
dotnet add package Endatix.Modules.Reporting.Contracts
```

## Recommended Usage:

For running and hosting the Endatix Platform, **Endatix.Api.Host** is the recommended main package as it simplifies the installation and setup process.

```bash
dotnet add package Endatix.Api.Host
```

You rarely reference this package directly — it arrives transitively through `Endatix.Api`.

## The extension point

`Endatix.Api`'s submission export endpoint (`POST forms/{formId}/submissions/export`) takes `IExportFormatRepository` and `IExportCapabilityRegistry` as **optional** dependencies, defined here. When no implementation is registered, the endpoint degrades gracefully to the built-in export formats; when the Reporting module is active it resolves them and unlocks the BI-ready export pipeline.

That indirection is what lets the API layer stay independent of the Reporting implementation: reference this package to plug your own reporting implementation into the same endpoint.

## Contents

| Area | Types |
|------|-------|
| Export contracts | `ExportProfile`, `ExportColumnDefinition`, `ExportDeliveryFormat`, `ExportQueryOptions`, `ExportRequestFilters`, `ExportTarget`, `ExportFormatSettings`, `FlattenedExportRow` |
| Export extensibility | `IExportFormatRepository`, `IExportCapabilityRegistry`, `IColumnAliasTransformer`, `IColumnAliasTransformerRegistry`, `ColumnAliasProfile` |
| Repositories | `IReportingExportRepository` |
| Integration state | `SubmissionIntegrationSnapshotDto`, `SubmissionIntegrationStatusCodes` |

The implementation lives in **Endatix.Modules.Reporting**.

## More Information:
For detailed installation instructions, please visit [Endatix Installation Guide](https://docs.endatix.com/docs/getting-started/installation).
