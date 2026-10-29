# Endatix API Integration Tests — Agent Guidance

In-process `WebApplicationFactory` tests against real `Endatix.WebHost` startup. **Run / filter / matrix:** [`../README.md`](../README.md#integration-tests).

## Add an HTTP test

```csharp
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class MyFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    [Fact]
    public async Task My_scenario()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" }, ct);
        using HttpClient client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        HttpResponseMessage response = await client.GetAsync("/api/forms", ct);
        response.EnsureSuccessStatusCode();
    }
}
```

- **Background work is off** in every test host: the outbox relay asks a switch that is off, the jobs scheduler is built but not started, and the daily import sweep is not registered. Nothing a test leaves behind runs while the next test resets the database.
  - A test that waits for an outbox handler calls `fixture.BackgroundWork.DeliverOutboxOnceAsync(ct)`, which hands the shared host's outbox to the shared host's handlers in the call. It refuses while the relay is on or a relay batch is still running; a handler that throws fails it with the handler's own error and leaves the batch claimable for the next call.
  - A test that needs jobs calls `fixture.BackgroundWork.StartJobsAsync(ct)`; one that needs the relay running calls `StartRelay()`; `StartAsync` starts both. Start them on the shared host only: the audience test clients of a per-test host refuse to start a background import, and the staged-import helpers start jobs only, so outbox messages stay for `DeliverOutboxOnceAsync`. Reset the database first (`PrepareWorldAsync`): the reset clears the previous test's outbox messages and job rows, and starting the scheduler drops the triggers whose job rows are gone, keeping those of jobs this test enqueued. The class stops it in `DisposeAsync` with `StopAsync(CancellationToken.None)`, which switches everything off at once and then waits for the relay batch and the jobs already running — see `AudienceDirectoryFlowTests`.
  - A job firing while background work is off is vetoed and its trigger dropped, so a job enqueued just before a stop does not run after a restart in the same test.
- **Reporting endpoints:** the shared host runs with `ReportingModule` off. Open `await using ReportingWebHost host = new(fixture)` in the test and prepare the world on it. Leave its background work off and trigger Reporting work over HTTP (compile, backfill) rather than wait for outbox handlers.
- **DB-only** (no host): `DbIntegrationTestCollection` + `DbIntegrationFixture` — see `ReportingQueryFilterTests`.
- **Provider-specific only when required:** `[Trait("DbSpecific", "PostgreSql")]` or `SqlServer` — e.g. `OutboxCaptureTests`, `SqlServerMigrationArtifactTests`.
- **Keycloak:** `IClassFixture<KeycloakTestContainerFixture>`, `Category=Keycloak`, `Priority=P2`.
- **Standard seed** (`StandardSeedOptions`, `ResetDatabaseAsync(useStandardSeed: true)`) is a realistic baseline; do not use it where the test needs an empty database.

## Reference tests

| Test                                  | Pattern                                                        |
| ------------------------------------- | -------------------------------------------------------------- |
| `AuthLoginFlowTests`                  | CriticalPath, `PrepareWorldAsync`, `IntegrationAuthMode.Login` |
| `FormNotFoundProblemDetailsFlowTests` | FeatureFlow, 404 + problem+json on missing form / definitions (`type`/`instance`/`traceId`) |
| `FormValidationProblemDetailsFlowTests` | FeatureFlow, FE FluentValidation 400 + problem+json with `fields` (not FE ErrorResponse) |
| `ProblemDetailsContractFlowTests`     | FeatureFlow, unhandled 500 / export / 409 conflict + documented empty-body 401/403 |
| `HealthCheckTests`                    | Host smoke, provider-agnostic                                  |
| `StartupMigrationTests`               | DB-only, provider-agnostic migrations                          |
| `SqlServerMigrationArtifactTests`     | `DbSpecific=SqlServer` stored proc / seed checks               |
| `ReportingQueryFilterTests`           | `DbSpecific=PostgreSql` module schema                          |
| `ExportFormatSeedIntegrationTests`    | Db-only PG, `SeedDefaultsAsync` (named tenant filter, unique name, mapping repair) |

## Shared infrastructure (`Endatix.IntegrationTests.Shared`)

Consumed by SaaS integration tests. Keep module-agnostic.

```text
EndatixTestcontainers → DatabaseInfrastructureFixture → DbIntegrationFixture
                                                      ↘ EndatixIntegrationWebHostFixture (IntegrationTests project)
```

- One DB container per process (`EndatixTestcontainers`).
- Provider from `ENDATIX_TEST_DB_PROVIDER`.
- Module helpers (e.g. `ReportingTestSchema`, `IntegrationTenantContext`) live in **IntegrationTests**, not Shared: Shared ships as the **`Endatix.IntegrationTesting`** package, so anything added there becomes public API, and its NSubstitute is `PrivateAssets="all"` — Shared helpers are plain classes, never `Substitute.For<>` factories.
- Tenant for a hand-built DbContext: `new IntegrationTenantContext(id)`, which rejects 0 so isolation tests use two real tenants. `IntegrationTenantContext.Bypass` (tenant 0) turns the query filter off for DbContext reads only — Core handlers reject it. Substitute an `ITenantContext` only where `TenantId` changes mid-test (`OutboxCaptureTests`).
- DB schema assertions: `IntegrationDbAssert` in Shared.
- After Shared changes, build `Endatix.IntegrationTests` and `Endatix.SaaS.IntegrationTests` in the monorepo.
