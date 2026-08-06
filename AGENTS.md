# Endatix API (OSS) — Agent Instructions

Clean Architecture + vertical slices in `oss/`. Architecture/testing rules of record: [`.cursor/rules/endatix-api-rules.mdc`](../.cursor/rules/endatix-api-rules.mdc). Integration suite ops: [`tests/README.md`](tests/README.md).

## Unit vs integration

**Do not duplicate the full decision tree here.** Use:

| Need | Doc |
| --- | --- |
| When unit vs integration, naming (`UnitOfWork_Scenario_ExpectedBehavior`), AAA | [endatix-api-rules.mdc → Testing](../.cursor/rules/endatix-api-rules.mdc) |
| Testcontainers, Respawn, traits (`Category` / `Priority` / `DbSpecific`), how to run | [`tests/README.md`](tests/README.md) |

**Short rule of thumb**

* **Unit** — domain, handlers, validators, mappers, endpoint `ExecuteAsync` mapping; mocks/substitutes only.
* **Integration** — HTTP + auth + EF + real DB (`WebApplicationFactory` / Testcontainers). Prefer `CriticalPaths/` · `FeatureFlows/` · `Infrastructure/`.

## Unit test placement (OSS)

| Layer | Project | Folder | Reference |
| --- | --- | --- | --- |
| FastEndpoints | `Endatix.Api.Tests` | `Endpoints/{Feature}/` | `Forms/DeleteTests.cs`, `DataLists/AddLocaleTests.cs` |
| Handlers / commands | `Endatix.Core.Tests` | `UseCases/{Feature}/{Action}/` | `Forms/Delete/DeleteFormHandlerTests.cs`, `DataLists/Locales/AddDataListLocale*Tests.cs` |
| Domain | `Endatix.Core.Tests` | `Entities/` | `DataListLocaleCatalogTests.cs` |
| Infrastructure | `Endatix.Infrastructure.Tests` | Mirror source | `Data/Querying/...` |

* **Class:** `{Sut}Tests`. **Methods:** `Method_State_ExpectedBehavior`.
* Always `// Arrange` · `// Act` · `// Assert`.

### FastEndpoints (`Endatix.Api.Tests`)

Pattern: substitute `IMediator` → `Factory.Create<TEndpoint>(_mediator)` → assert `response.Result`.

Minimum cases: invalid → 400 · not found → 404 (if applicable) · success payload · request→command via `Received`/`Arg.Is`. Skip FluentValidation re-tests unless validation is the SUT.

### Handlers (`Endatix.Core.Tests`)

Substitute `IRepository<T>` (+ `IMediator` if publishing). Cover: not found · happy path + persist · domain `Invalid` (no persist) · event reason/payload. Prefer real aggregates. Thin command-ctor tests when `Guard.Against.*` matters.

## Run (examples)

```bash
# From oss/
dotnet test tests/Endatix.Api.Tests/Endatix.Api.Tests.csproj --filter "FullyQualifiedName~AddLocaleTests"
dotnet test tests/Endatix.Core.Tests/Endatix.Core.Tests.csproj --filter "FullyQualifiedName~AddDataListLocale"
# Integration: see tests/README.md
```

## Related

* SaaS / Hub agent rules: [`../.cursor/AGENTS.md`](../.cursor/AGENTS.md)
* Integration contributor notes: `tests/Endatix.IntegrationTests/AGENTS.md` (linked from `tests/README.md`)
