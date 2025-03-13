---
sidebar_position: 5
title: Health Checks
---

# Health Checks Configuration

Health checks are an important part of any application for monitoring its health and dependencies. Endatix provides built-in health check functionality that is configured automatically by default.

## Default Configuration

When you use `builder.Host.ConfigureEndatix()`, the following health checks are automatically configured:

- **Self-checks**: Basic application health check
- **Process health**: Memory usage monitoring
- **Disk space**: Checking available disk space (where supported)
- **Database**: Connection to your configured database

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

### Database-Specific Health Checks

Endatix automatically adds the appropriate health check for your configured database provider:

```csharp
// SQL Server health check
endatix.HealthChecks.AddSqlServer(
    connectionString,
    healthQuery: "SELECT 1;",
    name: "database", 
    tags: new[] { "db", "sql" });

// PostgreSQL health check
endatix.HealthChecks.AddNpgSql(
    connectionString,
    healthQuery: "SELECT 1;",
    name: "database",
    tags: new[] { "db", "postgresql" });
```

### Disabling Health Checks

If you need to completely disable health checks:

```csharp
builder.Host.ConfigureEndatix(endatix => {
    endatix.SkipHealthChecks();
    
    // Rest of your configuration...
});
```

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

// Then register it:
endatix.HealthChecks.AddTypeActivatedCheck<MyServiceHealthCheck>("my-service");
```

## UI Enhancements

For production environments, you might want to consider adding the [HealthChecks.UI](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks#healthchecksui) package which provides a more comprehensive dashboard for monitoring your application health. 