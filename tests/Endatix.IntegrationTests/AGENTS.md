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

| Test | Pattern |
| --- | --- |
| `AuthLoginFlowTests` | CriticalPath, `PrepareWorldAsync`, `IntegrationAuthMode.Login` |
| `HealthCheckTests` | Host smoke, provider-agnostic |
| `StartupMigrationTests` | DB-only, provider-agnostic migrations |
| `SqlServerMigrationArtifactTests` | `DbSpecific=SqlServer` stored proc / seed checks |
| `ReportingQueryFilterTests` | `DbSpecific=PostgreSql` module schema |

## Shared infrastructure (`Endatix.IntegrationTests.Shared`)

Consumed by SaaS integration tests. Keep module-agnostic.

```text
EndatixTestcontainers → DatabaseInfrastructureFixture → DbIntegrationFixture
                                                      ↘ EndatixIntegrationWebHostFixture (IntegrationTests project)
```

- One DB container per process (`EndatixTestcontainers`).
- Provider from `ENDATIX_TEST_DB_PROVIDER`.
- Module helpers (e.g. `ReportingTestSchema`) live in **IntegrationTests**, not Shared.
- DB schema assertions: `IntegrationDbAssert` in Shared.
- After Shared changes, build `Endatix.IntegrationTests` and `Endatix.SaaS.IntegrationTests` in the monorepo.
