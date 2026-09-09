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

- **DB-only** (no host): `DbIntegrationTestCollection` + `DbIntegrationFixture` — see `ReportingQueryFilterTests`.
- **Provider-specific only when required:** `[Trait("DbSpecific", "PostgreSql")]` or `SqlServer` — e.g. `OutboxCaptureTests`, `SqlServerMigrationArtifactTests`.
- **Keycloak:** `IClassFixture<KeycloakTestContainerFixture>`, `Category=Keycloak`, `Priority=P2`.

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
