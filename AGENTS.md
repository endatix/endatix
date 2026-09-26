# Endatix API (OSS) — Agent Instructions

Rules of record for .NET code in this repo; the SaaS workspace follows them too. Layering, modules, MediatR vs direct reads, paged lists, events and multi-tenancy: [`ARCHITECTURE.md`](ARCHITECTURE.md). Integration suite ops: [`tests/README.md`](tests/README.md).

## C# conventions

- Primary constructors for DI. `sealed` by default; `internal` unless the type is public API or framework-discovered (endpoints, requests, validators).
- `is null` / `is not null`. `var`, unless an explicit type reads better (`Foo bar = new();`). Records for immutable data.
- Comment a decision, constraint or non-obvious invariant, not what the name already says.

## Endpoints (FastEndpoints)

- **One file per operation.** `Endpoints/{Feature}/{Op}.cs` holds the endpoint, request, response and validator. Split a part into `{Op}.{Part}.cs` only when it is large or reused and the split makes the endpoint easier to read. Older split endpoints (`Forms/Delete.*`) merge when touched, not in bulk. References: `Forms/List.cs`, `Public/Tenants/GetBySlug.cs`.
- Declare access straight after the verb ([Endpoint authorization](#endpoint-authorization-api)). Return `Results<Ok|Created<T>, ProblemHttpResult>` via `TypedResultsBuilder` ([Error HTTP contract](#error-http-contract-api)).
- **Anonymous:** `Group<PublicApiGroup>()` adds `public/`, so the route must not repeat it. Pair `AllowAnonymous()` with `Throttle(hits, seconds)` (keys on `X-Forwarded-For`, then the connection IP). An endpoint that must not reveal whether a value exists returns the same payload for found, duplicate and bot input, and keeps timing-visible work behind it. Reference: `Public/Tenants/GetBySlug.cs`.

## Validation

- FluentValidation on every request. Limits come from constants (`PagedRequestLimits`, entity `*MaxLength`), never literals.
- Custom rules are `IRuleBuilder` extensions built on `Must` — FluentValidation's recommended pattern (`Common/FluentValidationExtensions.cs`, e.g. `ValidUrlSlug`). `Custom(...)` only when one rule adds several failures.
- A field the entity also guards: the entity exposes `static string? {Field}Error(value)` (message or `null`) and throws it with `DomainValidationException.ThrowIfError`; the validator reports that message through a `Must` extension with a message placeholder. Never restate limits in the validator or try/catch the factory.
- **Paged lists:** `Include` the shared validators (`SearchablePagedRequestValidator` when the list has search, otherwise `PageableRequestValidator` plus the `PagedRequestLimits.MAX_PAGE_SIZE` cap; `SortableRequestValidator<T>`). Map with `ListRequestExtensions`. Handlers take `SearchablePageRequest` / `SortRequest<T>` and do not re-apply those limits. Do not `Skip` the raw page: count, then `Paged<T>.ResolvePage`, then skip that window (`PageRequest.Skip` overflows on a huge page). One helper should own that sequence. Reference: `Endpoints/Forms/List.cs` (validators), `UseCases/Themes/List/ListThemesHandler.cs` (`ResolvePage`). See [ARCHITECTURE.md → Paged list requests](ARCHITECTURE.md#paged-list-requests).

## Data access

- Generic `IRepository<T>` + Ardalis specifications. A dedicated repository (`Core.Abstractions.Repositories` → `Infrastructure.Data.Repositories`) only for multi-entity aggregates or queries a spec cannot express.
- `IUnitOfWork` only when several entities must save atomically: begin → changes → one `SaveChangesAsync` → commit. In `catch`: roll back, log, return author-written `Result.Error` (never `ex.Message`).
- Global query filters are **named** (`EndatixQueryFilterNames`); opt out by name, never blanket. Ambient tenant `0` is bypass, not isolation. See [ARCHITECTURE.md → Multi-tenancy](ARCHITECTURE.md#multi-tenancy-platform-tenants).

## Entity Ids (snowflake)

Client snowflake stamped on `Add` by `ApplySnowflakeIdValueGenerators` — not IDENTITY, not `IIdGenerator` in handlers/repos. `Create(args)` leaves `Id == 0`; `Create(long id, args)` only for tests/seed/import. Model maps `SnowflakeValueGeneratorFactory`; `IIdGenerator<long>` is resolved at `Add` from host DI (do not capture a generator instance in `OnModelCreating`). NSubstitute `AddAsync` must stamp `Id` itself (`ci.Arg<T>().Id = SomeId`). Details: [`ARCHITECTURE.md` → Entity Ids](ARCHITECTURE.md).

## Error HTTP contract (API)

Canonical JSON for **all** API errors (handler `ToProblem`, FluentValidation, unhandled exceptions, export stream errors):

```json
{
  "type": "https://www.rfc-editor.org/rfc/rfc9110.html#name-400-bad-request",
  "title": "There was a problem with your request",
  "status": 400,
  "detail": "Name is required.",
  "instance": "/api/forms",
  "traceId": "0HMPNHL0JHL76:00000001",
  "errorCode": "NotEmptyValidator",
  "fields": { "name": ["Name is required."] }
}
```

- Handler failures → RFC7807 `application/problem+json` via `TypedResultsBuilder` + `ProblemHttpResult` / `ToProblem`.
- `ToProblem` maps Invalid → 400, NotFound → 404, Conflict → 409, Unauthorized → 401, Forbidden → 403, Error/CriticalError → 500, Unavailable → 503.
- **`detail` is always a non-empty string**, falling back to the title when the result carries no errors. Consumers treat it as required (Hub's `ProblemDetailsSchema` types it non-optional) — never emit `"detail": ""`.
- FastEndpoints FluentValidation uses `c.Errors.ResponseBuilder` → `EndatixProblemDetails` (`fields` dictionary). Do **not** use stock `UseProblemDetails()` (wrong `errors` array shape).
- Unhandled exceptions → `EndatixExceptionHandler` (`IExceptionHandler`); 500 body never includes exception text. It is a safety net, not a status mapper — see below.
- **No 5xx body ever echoes handler- or exception-derived text.** `EndatixProblemDetails.Create` replaces any `>= 500` detail with the generic title and logs the original (correlate via `traceId`). Never pass `ex.Message` to a `Result.*` factory — including `Result.Error`: the 5xx scrub is defense in depth, not a license (see below). Log the exception, return author-written text. If a 5xx message must reach the user, model the failure as a 4xx/503 instead.
- Writing a problem body by hand? Pass `contentType: "application/problem+json"` to `WriteAsJsonAsync`; it otherwise overwrites `Response.ContentType` with `application/json`.
- `SetErrorMessage(...)` overrides the problem **title for every status**, so set it only on the branch it describes (see `Auth/VerifyEmail.cs`) — otherwise a 404 inherits a 400-shaped message.
- OpenAPI: `Description(b => b.Produces<T>(...).ProducesProblem(400).ProducesProblem(404))` listing exactly the statuses the endpoint's `Summary(s => s.Responses[...])` declares — every endpoint returning `ProblemHttpResult` must have one. The success status matches the typed result (`Ok` → 200, `Created` → 201). FE validators set `ProducesMetadataType = typeof(ProblemDetails)`.

### Exception text never reaches the caller (`IEndUserSafeError` / `SafeError`)

`Result` error messages become RFC7807 `detail`, and 4xx `detail` is echoed verbatim. `ex.Message` — EF Core, Npgsql, `JsonException`, any BCL guard — carries connection strings, SQL and file paths, so it must never reach a `Result.*` factory or a `ValidationError`. `ResultFactoryMustNotInterpolateExceptionMessageTests` fails the build on it.

Failure travels one way, and only `ToProblem` maps status:

| Layer                 | Signals failure by                                               | Becomes                                        |
| --------------------- | ---------------------------------------------------------------- | ---------------------------------------------- |
| Entity / value object | `throw Domain*Exception` (a void invariant has no other channel) | caught by its handler                          |
| Handler / use case    | `return Result.Invalid / NotFound / Conflict / Error`            | `ToProblem` → status + `fields`                |
| Anything uncaught     | —                                                                | opaque 500, logged (`EndatixExceptionHandler`) |

The handler is the conversion point, and `SafeError` is how it recovers an author-written message there:

```csharp
// Domain (Endatix.Core/Exceptions): DomainValidationException : ArgumentException,
// DomainRuleException : InvalidOperationException — both IEndUserSafeError, so existing catches still work.
throw new DomainRuleException($"A data list cannot have more than {MaxAvailableCultures} cultures.");

// Handler: no ex.Message, no re-derived reason, no severity decision.
catch (ArgumentException ex)
{
    return Result.Invalid(new ValidationError
    {
        Identifier = nameof(Query.Locale),
        ErrorMessage = SafeError.LogAndResolve(logger, ex, "Invalid locale.", $"searching data list {id}")
    });
}
```

- `SafeError.MessageOr` / `LogAndResolve` are the **only** places `EndUserMessage` may be read. `LogAndResolve` also picks the severity: `Information` for an opted-in rejection, `Error` **with the exception** otherwise.
- **Never re-derive the reason in the handler** by re-inspecting input or sniffing `ex.Message` — that duplicates the domain's conditions and the copies drift. `ParamName` is for attribution (which field), never for the message.
- **Never opt in a type that wraps a provider exception**, and never return `InnerException.Message`. `DomainValidationException` holds `EndUserMessage` separately because `ArgumentException` appends `" (Parameter 'x')"`.
- **Prefer a real message over a mask.** "…cannot have more than 25 cultures." is actionable; "Could not add locale." is not. No safe message to author? Log and return a static string (`DefaultAuthorizationMapper`, `ReCaptchaHttpClient`, `ThemeJsonData`).
- **The boundary is not a status mapper.** A `Domain*Exception` reaching `EndatixExceptionHandler` is a missing `catch`, and gets an opaque 500 so the defect surfaces. Prefer a Result-returning domain API where input is caller-supplied and validated in a loop (`DataListEnsureLocales.TryEnsure`) — it keeps throws off hot paths entirely.

### Rethrowing

- Log **or** rethrow, never both (Sonar S2139). Wrap with context and keep the original as `InnerException`; bare `throw;` only to rethrow unchanged (`OperationCanceledException`).
- Log where the outcome is owned: return `Result.Error`, mark a row failed, or let the outbox relay log once.

### Uniform failure responses (OWASP A07)

Account-facing failures must be **indistinguishable to the caller**: same status, same message, whatever the real cause. Log the real reason server-side instead — never return it.

- Applies to: login, registration, forgot/reset password, send-verification-email, verify-email, invite activation. Unknown account, unconfirmed email and wrong password all collapse to **one** `Result.Invalid` with one message.
- Normalize at the **handler/service boundary**, not at the endpoint. `IAuthService` / `IUserPasswordManageService` are public abstractions — an external IdP implementation returning `NotFound` must not become a 404 that a wrong password would not produce.
- **Do not `ToErrorResult<T>()` / propagate an upstream status on a credential path.** It is correct for post-authentication infrastructure errors (e.g. session persistence), which reveal nothing about the account.
- Token flows (verify-email, invite activation) answer **400 for every token failure** - unknown, dangling, expired, used. Never `NotFound`: a 404-vs-400 split tells the caller whether the token ever existed.
- Equalize work, not just payloads: run the password KDF on the unknown-account path too (`AuthService.BurnPasswordHashingWork`), or response time leaks what the body does not.
- Pair each with a `#region Security and Privacy Tests` test asserting the disallowed strings are absent. References: `LoginHandler.INVALID_CREDENTIALS_MESSAGE`, `EmailVerificationService.INVALID_VERIFICATION_TOKEN_MESSAGE`, `ForgotPasswordHandler.GENERAL_SUCCESS_MESSAGE`, `SendVerificationEmailHandler` (returns `Success` for unknown users), `UserPasswordManageServiceTests` (`DoesNotLeakUserExistence`).

## Endpoint authorization (API)

- **Every endpoint declares its access** straight after the verb: `Permissions(...)`, `Roles(...)`, `Policies(...)` or `AllowAnonymous()`. Module endpoints discovered through `IHasFastEndpoints` included. The default policy alone (authenticated + `sub`) is only acceptable where the endpoint is scoped to the caller by construction — `Auth/Me`, `Auth/Logout`, `MyAccount/ChangePassword`.
- **Admins pass every permission check.** `AssertionPermissionsHandler` lets any user whose roles make `IsAdmin` true (Admin, PlatformAdmin) through the FastEndpoints permission requirement before grants are read, so an endpoint needs no admin grant. Seed `RolePermissions` rows only for the non-admin roles that should reach it.
- **Tenant-owned reads pass the tenant explicitly** and refuse `<= 0` — see [ARCHITECTURE.md → Multi-tenancy → Data isolation](ARCHITECTURE.md#multi-tenancy-platform-tenants).

## Transactional email

- Compose `EmailWithTemplate` with `To`, `TemplateId` and `Metadata` only. **Do not set `From`** on stored templates: `EmailTemplateRenderer` resolves it (`Endatix:EmailTemplates:*:FromAddress` → template row → default). External templates (`IsExternal = true`) send `From` as-is.
- `Metadata` keys match the template's `{{placeholders}}` (`Scripts/Data/*.sql`); a template id with no row throws at render time.
- Best-effort after a committed write: catch and log a send failure, do not turn it into a 5xx. When nothing was persisted, return `Result.Unavailable` (`ForgotPasswordHandler`).

## Testing

- **Unit** — domain, handlers, validators, mappers, endpoint `ExecuteAsync` mapping; substitutes only.
- **Integration** — HTTP + auth + DI + EF + real database (`WebApplicationFactory`, Testcontainers, Respawn; never an in-memory DB). Assert status, response contract and the persistence effect. Authenticate with synthetic JWT; Keycloak only in trait-scoped tests. Fixtures, traits and folders (`CriticalPaths/` · `FeatureFlows/` · `Infrastructure/`): [`tests/README.md`](tests/README.md).
- **Naming:** class `{Sut}Tests`. Unit and persistence-integration methods are `UnitOfWork_Scenario_ExpectedBehavior` (`Constructor_NegativeOrZeroFormId_ThrowsArgumentException`, `GetByFormIdAsync_WithOtherTenant_ReturnsNull`). HTTP integration methods read as outcomes (`Create_tenant_as_tenant_admin_is_forbidden`).
- Explicit `// Arrange` · `// Act` · `// Assert`; one behavior per test.

### Unit test placement

| Layer               | Project                        | Folder                         | Reference                                                                      |
| ------------------- | ------------------------------ | ------------------------------ | ------------------------------------------------------------------------------ |
| FastEndpoints       | `Endatix.Api.Tests`            | `Endpoints/{Feature}/`         | `Forms/DeleteTests.cs`, `DataLists/*LocaleTests.cs`                            |
| Handlers / commands | `Endatix.Core.Tests`           | `UseCases/{Feature}/{Action}/` | `Forms/Delete/DeleteFormHandlerTests.cs`, `DataLists/Locales/*Locale*Tests.cs` |
| Domain              | `Endatix.Core.Tests`           | `Entities/`                    | `DataListLocaleCatalogTests.cs`                                                |
| Infrastructure      | `Endatix.Infrastructure.Tests` | Mirror source                  | `Data/Querying/...`                                                            |

#### FastEndpoints (`Endatix.Api.Tests`)

Pattern: substitute `IMediator` → `Factory.Create<TEndpoint>(_mediator)` → assert `response.Result`.

Minimum cases: invalid → 400 · not found → 404 (if applicable) · success payload · request→command via `Received`/`Arg.Is`. Skip FluentValidation re-tests unless validation is the SUT.

**Error HTTP contract:** resource endpoints return `Results<Ok|Created<T>, ProblemHttpResult>`. Assert failures as:

```csharp
var problemResult = response.Result as ProblemHttpResult;
problemResult.Should().NotBeNull();
problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound); // or 400
```

Do **not** assert empty-body `BadRequest` / `NotFound`. References: `FormDefinitions/GetActiveTests.cs`, `Forms/Delete.cs` (OpenAPI `Produces` + `ProducesProblem`).

#### Handlers (`Endatix.Core.Tests`)

Substitute `IRepository<T>` (+ `IMediator` if publishing). Cover: not found · happy path + persist · domain `Invalid` (no persist) · event reason/payload. Prefer real aggregates. Thin command-ctor tests when `Guard.Against.*` matters.

## Run (examples)

```bash
# From oss/
dotnet test tests/Endatix.Api.Tests/Endatix.Api.Tests.csproj --filter "FullyQualifiedName~AddLocaleTests"
dotnet test tests/Endatix.Core.Tests/Endatix.Core.Tests.csproj --filter "FullyQualifiedName~AddDataListLocale"
# Integration: see tests/README.md
```

## Embed playground (DevTools)

`GET /dev/embed-host` on WebHost (on in Development; 404 in Production unless `Endatix:DevTools:EmbedHost:Enabled=true`). **Query is the contract** (agents use this; UI only GET-rewrites it):

| Query               | Meaning                                                                                                                        |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| `formId`            | Required to emit `embed.js`. Omitted → builder shell (200). Invalid on builder → 200 + banner. `view=bare` without a valid id → 400. |
| `view`              | `builder` (default) or `bare` (iframe only; visual/e2e).                                                                       |
| `heightMode`        | `fill` or omit (auto).                                                                                                         |
| `token` / `prefill` | `data-token` wins over `data-prefill`.                                                                                         |
| `hubBaseUrl`        | Override Hub origin. Must match configured origin (scheme+host+port), an `AllowedHubHosts` origin, or loopback in Development. Invalid on builder → 200 + banner; `view=bare` → 400. |

HTTPS playground + HTTP Hub = mixed content: **builder UI** (200, no script), including `view=bare` — recovery card, not a blank/plain 400. Recovery href = Kestrel's first `http://` bind (`IServerAddressesFeature`, `*`/`+` parsed by trailing port); no bind → no button, never a guessed `:5000`. Default launch profile is `:5000`. MapWhen is registered **before** HTTPS redirection so HTTP is not 307'd to `:5001`. Apply has no `action` (keeps PathBase). Bare links prefix `Request.PathBase`. Headers: `Cache-Control: no-store`, `Referrer-Policy: no-referrer`, `X-Robots-Tag: noindex`. Anonymous when enabled (third-party host; Hub cookies do not apply). Force-open Configure is not persisted. **Open in new tab** = same query + `view=bare`. Loopback only when `AllowLoopback` (Development default; do not set true in Production). User doc: `docs/endatix-docs/docs/guides/embed-form-via-iframe.mdx`.

## Related

- Integration contributor notes: `tests/Endatix.IntegrationTests/AGENTS.md` (linked from `tests/README.md`)
- Public docs writing rules (voice, cards, when a table is allowed): [`docs/endatix-docs/AGENTS.md`](docs/endatix-docs/AGENTS.md)
