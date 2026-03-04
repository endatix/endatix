---
sidebar_position: 5
title: Health Checks
---

# Health Checks Configuration

Health checks are an important part of any application for monitoring its health and dependencies. Endatix provides built-in health check functionality that is configured automatically by default.

This page focuses on **configuration and settings** (defaults, endpoint path, filtering, custom implementations). For **endpoints, JSON shape, and host/middleware API usage**, see [Health checks](/docs/developers/api/health-checks) in the API docs.

## Default Configuration

When you use `builder.Host.ConfigureEndatix()`, the following health checks are automatically configured:

- **Self**: Basic application health check (skipped when Aspire ServiceDefaults are present)
- **Database**: EF Core health check for the main app database when persistence is configured
- **Identity-database**: EF Core health check for the identity database when identity persistence is configured

These health checks are exposed through the following endpoints:

- **`/health`**: Basic health status (healthy, degraded, or unhealthy)
- **`/health/detail`**: Detailed JSON report of all health checks
- **`/health/ui`**: HTML UI showing health check results in a more readable format

## Customizing Health Checks

### Adding Custom Health Checks

You can add your own health checks to the default configuration:

```csharp
builder.Host.ConfigureEndatixWithDefaults(endatix => {
    // Add custom health checks
    endatix.HealthChecks
        .AddCheck("custom-service", () => HealthCheckResult.Healthy("Service is running"));
});
```

### Database health checks

Endatix adds EF Core DbContext health checks automatically when persistence is configured (e.g. `UseDefaults()` or `UseSqlServer<AppDbContext>()`). The checks are named `database` and `identity-database` and use tags `db` and `ready`. No extra configuration is required.

### Configuring Health Check Endpoints

You can customize the health check endpoints when configuring middleware:

```csharp
app.UseEndatix(options => {
    options.UseHealthChecks = true;
    options.HealthCheckPath = "/healthz"; // Changes base path from /health to /healthz
});
```

## Advanced Usage

### Filtering Health Checks

Health checks can be tagged for filtering. For example, to create an endpoint that only checks database health:

```csharp
app.UseEndatix();

// Add a custom health check endpoint that only shows database checks
app.UseHealthChecks("/health/database", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("db")
});
```

### Custom Health Check Implementation

For more complex health checks, you can implement the `IHealthCheck` interface:

```csharp
public class MyServiceHealthCheck : IHealthCheck
{
    private readonly IMyService _service;
    
    public MyServiceHealthCheck(IMyService service)
    {
        _service = service;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _service.IsHealthyAsync(cancellationToken);
            
            if (isHealthy)
            {
                return HealthCheckResult.Healthy("Service is healthy");
            }
            
            return HealthCheckResult.Degraded("Service is experiencing issues");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Service is unhealthy", ex);
        }
    }
}

// Then register it via the underlying builder (framework extension method):
endatix.HealthChecks.Builder.AddTypeActivatedCheck<MyServiceHealthCheck>("my-service");
```

## UI Enhancements

For production environments, you might want to consider adding the [HealthChecks.UI](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks#healthchecksui) package which provides a more comprehensive dashboard for monitoring your application health. 