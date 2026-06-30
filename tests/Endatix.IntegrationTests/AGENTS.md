# Endatix API Integration Tests — Agent Guidance

This project (`Endatix.IntegrationTests`) exercises the real `Endatix.WebHost` startup pipeline in-process via `WebApplicationFactory`, backed by Testcontainers (PostgreSQL or SQL Server) and Respawn for fast DB resets.

Shared infrastructure lives in `../Endatix.IntegrationTests.Shared/`. Human-oriented docs: `oss/docs/endatix-docs/docs/developers/integration-testing.mdx` and monorepo `tests/README.md`.

## Prerequisites

- **Docker** running locally.
- **.NET SDK** from `oss/global.json`.

```bash
dotnet test oss/tests/Endatix.IntegrationTests/Endatix.IntegrationTests.csproj -c Release --filter "Category!=Keycloak"
```

## Project layout

| Folder | Purpose |
| ------ | ------- |
| `CriticalPaths/` | Must-not-break flows (login, auth, core API contracts). |
| `FeatureFlows/` | Multi-step domain journeys (forms, submissions, tenants). |
| `Infrastructure/` | Host wiring, DB reset, seed smoke, module-specific DB tests, Keycloak smoke. |

Place new tests by **intent**, not by layer. One test class = one scenario or cohesive flow.

## Collections — pick one per test class

xUnit collections control fixture lifetime. **Never mix collections in one class.**

### `EndatixIntegrationTestCollection` (default)

Use for HTTP/API tests that need the full host.

- Fixture: `EndatixIntegrationWebHostFixture`
- Provides: shared DB container, `EndatixWebApplicationFactory`, `Checkpoint`, `Seed`, `CreateClient()`
- Reference: `CriticalPaths/Auth/AuthLoginFlowTests.cs`

```csharp
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class MyFlowTests
{
    private readonly EndatixIntegrationWebHostFixture _fixture;

    public MyFlowTests(EndatixIntegrationWebHostFixture fixture) => _fixture = fixture;
}
```

### `DbIntegrationTestCollection`

Use when testing EF/query filters/migrations **without** spinning up `WebApplicationFactory`.

- Fixture: `DbIntegrationFixture` (wraps `DatabaseInfrastructureFixture`)
- Reference: `Infrastructure/ReportingQueryFilterTests.cs`
- Module schema helpers belong in **this project** (e.g. `ReportingTestSchema`), not in Shared — keep Shared module-agnostic.

### Keycloak (P2, no collection)

Use `IClassFixture<KeycloakTestContainerFixture>` for Keycloak container smoke tests.

- Reference: `Infrastructure/KeycloakRealmSmokeTests.cs`
- Always tag `[Trait("Category", "Keycloak")]` and `[Trait("Priority", "P2")]` so default CI excludes them.

## Writing a new HTTP integration test

1. **Choose collection** — `EndatixIntegrationTestCollection` for API tests.
2. **Add traits** — `Category` + `Priority` (see below).
3. **Prepare world** — prefer `PrepareWorldAsync` over manual reset + seed.
4. **Act via HTTP** — use `HttpClient` from world personas or `_fixture.CreateClient()`.
5. **Assert** — status code + response contract + persistence side-effect (when relevant).
6. **Use cancellation** — `TestContext.Current.CancellationToken` in async tests.

```csharp
var cancellationToken = TestContext.Current.CancellationToken;

var world = await _fixture.PrepareWorldAsync(
    IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" },
    cancellationToken);

using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
var response = await client.GetAsync("/api/forms", cancellationToken);
response.EnsureSuccessStatusCode();
```

### World presets

| Preset | Use when |
| ------ | -------- |
| `IntegrationWorldOptions.Empty` | Reset only, no seed |
| `IntegrationWorldOptions.SingleTenant` | One tenant + standard roles |
| `IntegrationWorldOptions.MultiTenant` | Tenant isolation scenarios |

Override password: `with { DefaultPassword = "..." }`.

### Auth modes

| Mode | When |
| ---- | ---- |
| `IntegrationAuthMode.Login` | Full auth pipeline (CriticalPath auth tests) |
| `IntegrationAuthMode.SyntheticJwt` | Fast token for non-auth scenarios |

### Personas

`TestPersona.TenantAdmin`, `Creator`, `PlatformAdmin`, `Anonymous`, `CustomRole("name")`.

## Traits and CI filters

Constants in `IntegrationTestFilters` (`Endatix.IntegrationTests.Shared`):

| Constant | Expression | CI usage |
| -------- | ---------- | -------- |
| `Default` | `Category!=Keycloak` | `main` |
| `PrFast` | `Category!=Keycloak&Priority!=P2` | PRs |

**Category:** `CriticalPath`, `FeatureFlow`, `Infrastructure`, `Keycloak`

**Priority:** `P0` (must pass on PR), `P1` (important), `P2` (nightly/manual — Keycloak, heavy tests)

**DbSpecific:** `PostgreSql` or `SqlServer` when a test only applies to one provider.

When adding tests, set traits so PR CI stays fast. Update `oss/.github/workflows/build-ci.yml` only if filter policy changes — keep in sync with `IntegrationTestFilters`.

## Assertions

- Prefer **one primary behavior** per test method; name methods for the behavior (`Login_and_me_return_expected_roles_...`).
- Use **AAA** comments when arrange/act/assert blocks are non-trivial.
- For auth tests: exercise real login (`IntegrationAuthMode.Login`).
- For infrastructure: assert host health, seed counts, or DB state — not unrelated API surface.
- Do not assert implementation details unless the test targets infrastructure wiring.

## Parallelism

`xunit.runner.json` disables assembly and collection parallelism — **intentional**. All collections share one DB per `dotnet test` process; tests rely on `Checkpoint.ResetAsync` for isolation. Do not enable parallel execution against the shared DB without a new isolation strategy.

## Environment variables

| Variable | Default | Purpose |
| -------- | ------- | ------- |
| `ENDATIX_TEST_DB_PROVIDER` | `PostgreSql` | `PostgreSql` or `SqlServer` |
| `ENDATIX_TEST_HOST_MODE` | `ProductionProgram` | `ProductionProgram` or `DedicatedIntegrationHost` |
| `ENDATIX_TEST_REUSE_CONTAINERS` | off | Keep containers between local runs (dev only) |
| `ENDATIX_TEST_RUN_ID` | auto | Docker label for debugging containers |
| `ENDATIX_TEST_DEBUG` | off | Extra diagnostics where supported |

## Reference tests

| Test | Pattern |
| ---- | ------- |
| `AuthLoginFlowTests` | CriticalPath, `PrepareWorldAsync`, `IntegrationAuthMode.Login`, personas |
| `StandardSeedTests` | Infrastructure, seed verification |
| `HealthCheckTests` | Infrastructure, host smoke |
| `ReportingQueryFilterTests` | DB-only collection, Respawn + module schema |
| `KeycloakRealmSmokeTests` | P2 Keycloak fixture |

---

## Developing shared infrastructure (`Endatix.IntegrationTests.Shared`)

Shared code is consumed by this project and by `Endatix.SaaS.IntegrationTests` in the monorepo. Treat it as a **stable test SDK** — minimal surface, no product-module coupling unless unavoidable.

### Layering

```
EndatixTestcontainers          ← one DB + network per process (internal)
        ↓
DatabaseInfrastructureFixture  ← connection string, provider, checkpoint
        ↓
DbIntegrationFixture           ← DB-only xUnit facade
EndatixIntegrationWebHostFixture ← DB + WebApplicationFactory (in IntegrationTests project)
```

**Rules:**

- Put **Testcontainers session** logic in `EndatixTestcontainers` only — one container per process, labeled for Docker Desktop (`com.docker.compose.project`, `endatix.test.*`).
- Put **Respawn** logic in `DatabaseCheckpoint` — schema lists must include module schemas (`reporting`, etc.).
- Put **HTTP host** wiring in `EndatixWebApplicationFactory` / `EndatixIntegrationWebHostFixture` (IntegrationTests project), not Shared.
- Put **module-specific** migration/schema helpers in IntegrationTests (e.g. `ReportingTestSchema`), not Shared.
- Shared must not reference `Endatix.Modules.*` — keeps SaaS reuse clean.

### When to add to Shared vs IntegrationTests

| Add to Shared | Add to IntegrationTests |
| ------------- | ----------------------- |
| DB container session, checkpoints, seed builder | WebApplicationFactory, host fixture, collection definitions |
| World, personas, auth clients, filters | Module-specific schema helpers |
| Keycloak container fixture (P2) | Test classes and flow-specific helpers |

### Best practices for Shared changes

1. **Keep APIs small** — extension methods on `IIntegrationTestHostFixture` (`PrepareWorldAsync`, `ResetDatabaseAsync`) over fat fixtures.
2. **Env-driven config** — `IntegrationDatabaseSettings.FromEnvironment()`, not hardcoded connection strings.
3. **Idempotent session** — `AcquireDatabaseAsync` / `AcquireNetworkAsync` must be safe when multiple fixtures initialize in one process.
4. **Dispose is a no-op for session** — Ryuk cleans containers on process exit; fixtures call `ReleaseSessionAsync` for symmetry.
5. **Per-fixture checkpoint** — each fixture instance owns its `DatabaseCheckpoint`; do not share Respawn state across fixtures.
6. **Labels and names** — use `EndatixTestcontainerLabels` and stable container names for local debugging.
7. **Avoid `field` keyword pitfalls** — static lazy values need explicit `private static` backing fields, not C# 14 `field` in expression-bodied static properties.
8. **Exception param names** — `ArgumentOutOfRangeException` `paramName` must be a method parameter (`nameof(settings)`), not `nameof(settings.Provider)`.
9. **Test package versions** — use central versions from `oss/Directory.Packages.props`; do not duplicate packages in test csproj.
10. **Verify both consumers** — after Shared changes, build `Endatix.IntegrationTests` and `Endatix.SaaS.IntegrationTests` when working in the monorepo.

### Adding a new module's DB-only tests

1. Create schema helper in `Endatix.IntegrationTests` (migrate module DbContext against shared connection string).
2. Use `DbIntegrationTestCollection` + `DbIntegrationFixture`.
3. Reset via `_fixture.Checkpoint.ResetAsync(...)` at test start.
4. Tag `[Trait("DbSpecific", "PostgreSql")]` if provider-specific.
5. Do not add module project references to Shared — reference modules from IntegrationTests only.

### Adding Keycloak-related infrastructure

- Extend `KeycloakTestContainerFixture` in `Shared/KeycloakInfra/`.
- Join shared network via `EndatixTestcontainers.AcquireNetworkAsync`.
- Tag tests `Category=Keycloak`, `Priority=P2`.
- Never enable Keycloak in default `EndatixWebApplicationFactoryConfiguration` — tests that need it configure explicitly.
