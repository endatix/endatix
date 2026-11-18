# Endatix.Infrastructure

This package provides implementations of the application's dependencies and external resources. It includes infrastructure-level services like database access, messaging, and other integrations.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET. It is designed for building secure, scalable, and integrated form-centric applications that work with SurveyJS. Endatix empowers business users with advanced workflows, automation, and meaningful insights.

## Installation:

```bash
dotnet add package Endatix.Infrastructure
```

## Recommended Usage:

For running and hosting the Endatix Platform, **Endatix.Api.Host** is the recommended main package as it simplifies the installation and setup process.

```bash
dotnet add package Endatix.Api.Host
```

## Tenant entitlements

`ITenantEntitlements` answers whether a tenant may use a capability that a plan or licence can grant, such as
`Entitlements.BRANDING`. The default, `AllowAllTenantEntitlements`, answers yes for every tenant and every
entitlement, so a self-hosted deployment has every feature; its licence governs paid use.

A host that decides by plan registers its own scoped implementation, before or after Endatix's setup:

```csharp
builder.Services.AddScoped<ITenantEntitlements, PlanTenantEntitlements>();

public sealed class PlanTenantEntitlements(IPlanStore plans) : ITenantEntitlements
{
    public async Task<bool> HasAsync(long tenantId, string name, CancellationToken cancellationToken = default) =>
        (await plans.GetForTenantAsync(tenantId, cancellationToken)).Includes(name);
}
```

`IPlanStore` stands for the host's own plan lookup. A new paid capability adds a name to `Entitlements`.

## More Information:
For detailed installation instructions, please visit [Endatix Installation Guide](https://docs.endatix.com/docs/getting-started/installation).