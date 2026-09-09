# Endatix.IntegrationTesting

Test fixtures and harnesses for writing integration tests against the Endatix Platform. Provides Testcontainers-backed PostgreSQL, SQL Server and Keycloak infrastructure, database seeding with Respawn checkpointing between tests, and a `WebApplicationFactory` host fixture with ready-made authentication personas.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET. It is designed for building secure, scalable, and integrated form-centric applications that work with SurveyJS. Endatix empowers business users with advanced workflows, automation, and meaningful insights.

## Installation:

```bash
dotnet add package Endatix.IntegrationTesting
```

This is a test-only package — reference it from a test project, not from production code.

## What it provides:

- **`DbIntegrationFixture`** — a database-backed fixture that provisions a container, applies migrations and resets state between tests.
- **`IntegrationTestWorld`** / **`IntegrationSeedBuilder`** — declarative seeding of tenants, forms and submissions, with `StandardSeedPresets` for the common shapes.
- **`IIntegrationTestHostFixture`** — a host fixture over `WebApplicationFactory` with authentication wired up.
- **`TestPersona`** / **`IntegrationAuthClients`** — authenticated callers for permission and tenant-isolation tests.
- **`DatabaseCheckpoint`** — Respawn-based reset so each test starts from a known state.

Docker must be available on the machine running the tests: the fixtures start real PostgreSQL, SQL Server and Keycloak containers rather than using in-memory substitutes.

## More Information:
For detailed installation instructions, please visit [Endatix Installation Guide](https://docs.endatix.com/docs/getting-started/installation).
