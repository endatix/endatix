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
- OpenAPI: `Description(b => b.Produces<T>(...).ProducesProblem(400).ProducesProblem(404))` listing exactly the statuses the endpoint's `Summary(s => s.Responses[...])` declares — every endpoint returning `ProblemHttpResult` must have one. FE validators set `ProducesMetadataType = typeof(ProblemDetails)`.

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

### Uniform failure responses (OWASP A07)

Account-facing failures must be **indistinguishable to the caller**: same status, same message, whatever the real cause. Log the real reason server-side instead — never return it.

- Applies to: login, registration, forgot/reset password, send-verification-email, verify-email, invite activation. Unknown account, unconfirmed email and wrong password all collapse to **one** `Result.Invalid` with one message.
- Normalize at the **handler/service boundary**, not at the endpoint. `IAuthService` / `IUserPasswordManageService` are public abstractions — an external IdP implementation returning `NotFound` must not become a 404 that a wrong password would not produce.
- **Do not `ToErrorResult<T>()` / propagate an upstream status on a credential path.** It is correct for post-authentication infrastructure errors (e.g. session persistence), which reveal nothing about the account.
- Token flows (verify-email, invite activation) answer **400 for every token failure** - unknown, dangling, expired, used. Never `NotFound`: a 404-vs-400 split tells the caller whether the token ever existed.
- Equalize work, not just payloads: run the password KDF on the unknown-account path too (`AuthService.BurnPasswordHashingWork`), or response time leaks what the body does not.
- Pair each with a `#region Security and Privacy Tests` test asserting the disallowed strings are absent. References: `LoginHandler.INVALID_CREDENTIALS_MESSAGE`, `EmailVerificationService.INVALID_VERIFICATION_TOKEN_MESSAGE`, `ForgotPasswordHandler.GENERAL_SUCCESS_MESSAGE`, `SendVerificationEmailHandler` (returns `Success` for unknown users), `UserPasswordManageServiceTests` (`DoesNotLeakUserExistence`).

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
