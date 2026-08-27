# Endatix API (OSS) — Agent Instructions

Clean Architecture + vertical slices in `oss/`. Architecture/testing rules of record: [`.cursor/rules/endatix-api-rules.mdc`](../.cursor/rules/endatix-api-rules.mdc). Integration suite ops: [`tests/README.md`](tests/README.md).

## Unit vs integration

**Do not duplicate the full decision tree here.** Use:

| Need                                                                                 | Doc                                                                       |
| ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------- |
| When unit vs integration, naming (`UnitOfWork_Scenario_ExpectedBehavior`), AAA       | [endatix-api-rules.mdc → Testing](../.cursor/rules/endatix-api-rules.mdc) |
| Testcontainers, Respawn, traits (`Category` / `Priority` / `DbSpecific`), how to run | [`tests/README.md`](tests/README.md)                                      |

**Short rule of thumb**

- **Unit** — domain, handlers, validators, mappers, endpoint `ExecuteAsync` mapping; mocks/substitutes only.
- **Integration** — HTTP + auth + EF + real DB (`WebApplicationFactory` / Testcontainers). Prefer `CriticalPaths/` · `FeatureFlows/` · `Infrastructure/`.

## Unit test placement (OSS)

| Layer               | Project                        | Folder                         | Reference                                                                      |
| ------------------- | ------------------------------ | ------------------------------ | ------------------------------------------------------------------------------ |
| FastEndpoints       | `Endatix.Api.Tests`            | `Endpoints/{Feature}/`         | `Forms/DeleteTests.cs`, `DataLists/*LocaleTests.cs`                            |
| Handlers / commands | `Endatix.Core.Tests`           | `UseCases/{Feature}/{Action}/` | `Forms/Delete/DeleteFormHandlerTests.cs`, `DataLists/Locales/*Locale*Tests.cs` |
| Domain              | `Endatix.Core.Tests`           | `Entities/`                    | `DataListLocaleCatalogTests.cs`                                                |
| Infrastructure      | `Endatix.Infrastructure.Tests` | Mirror source                  | `Data/Querying/...`                                                            |

- **Class:** `{Sut}Tests`. **Methods:** `Method_State_ExpectedBehavior`.
- Always `// Arrange` · `// Act` · `// Assert`.

### FastEndpoints (`Endatix.Api.Tests`)

Pattern: substitute `IMediator` → `Factory.Create<TEndpoint>(_mediator)` → assert `response.Result`.

Minimum cases: invalid → 400 · not found → 404 (if applicable) · success payload · request→command via `Received`/`Arg.Is`. Skip FluentValidation re-tests unless validation is the SUT.

**Error HTTP contract:** resource endpoints return `Results<Ok|Created<T>, ProblemHttpResult>`. Assert failures as:

```csharp
var problemResult = response.Result as ProblemHttpResult;
problemResult.Should().NotBeNull();
problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound); // or 400
```

Do **not** assert empty-body `BadRequest` / `NotFound`. References: `FormDefinitions/GetActiveTests.cs`, `Forms/Delete.cs` (OpenAPI `Produces` + `ProducesProblem`).

### Handlers (`Endatix.Core.Tests`)

Substitute `IRepository<T>` (+ `IMediator` if publishing). Cover: not found · happy path + persist · domain `Invalid` (no persist) · event reason/payload. Prefer real aggregates. Thin command-ctor tests when `Guard.Against.*` matters.

## Error HTTP contract (API)

- Handler failures → RFC7807 `application/problem+json` via `TypedResultsBuilder` + `ProblemHttpResult`.
- `ToProblem` maps Invalid → 400, NotFound → 404, Conflict → 409, Unauthorized → 401, Forbidden → 403, Error/CriticalError → 500, Unavailable → 503.
- **`detail` is always a non-empty string**, falling back to the title when the result carries no errors. Consumers treat it as required (Hub's `ProblemDetailsSchema` types it non-optional) — never emit `"detail": ""`.
- `SetErrorMessage(...)` overrides the problem **title for every status**, so set it only on the branch it describes (see `Auth/VerifyEmail.cs`) — otherwise a 404 inherits a 400-shaped message.
- OpenAPI: `Description(b => b.Produces<T>(...).ProducesProblem(400).ProducesProblem(404))` listing exactly the statuses the endpoint's `Summary(s => s.Responses[...])` declares — every endpoint returning `ProblemHttpResult` must have one.
- **Known gap:** FastEndpoints request-validation failures still return FE's `{statusCode, message, errors}` shape, not problem+json. Closing that needs `c.Errors.UseProblemDetails()` in `ApiApplicationBuilderExtensions` — tracked separately; don't assume a 400 is RFC7807 yet.

## Run (examples)

```bash
# From oss/
dotnet test tests/Endatix.Api.Tests/Endatix.Api.Tests.csproj --filter "FullyQualifiedName~AddLocaleTests"
dotnet test tests/Endatix.Core.Tests/Endatix.Core.Tests.csproj --filter "FullyQualifiedName~AddDataListLocale"
# Integration: see tests/README.md
```

## Related

- SaaS / Hub agent rules: [`../.cursor/AGENTS.md`](../.cursor/AGENTS.md)
- Integration contributor notes: `tests/Endatix.IntegrationTests/AGENTS.md` (linked from `tests/README.md`)
