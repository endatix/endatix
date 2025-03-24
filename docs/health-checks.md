# Health Checks

Endatix provides health check capabilities to monitor the health of your application and its dependencies. Health checks are automatically configured when you use the default configuration, but you can customize them to suit your specific needs.

## Basic Configuration

Health checks are automatically configured when you use the default Endatix configuration:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureEndatix();
var app = builder.Build();
app.UseEndatixMiddleware();
app.Run();
```

This adds a basic self-check and exposes health checks at the `/health` endpoint.

## Custom Configuration

You can customize health checks using the `WithHealthChecks` method:

```csharp
builder.Host.ConfigureEndatix(endatix => endatix
    .WithHealthChecks(health => health
        .UseDefaults() // Adds a basic self-check
        .AddCheck("custom-check", () => HealthCheckResult.Healthy("Custom check is healthy"));
```

## Available Health Checks

Endatix provides several built-in health checks:

- **Self Check**: A basic check that always returns healthy
- **System Check**: Monitors CPU, memory, and disk usage
- **URI Check**: Checks if an external URI is accessible

## Custom Health Checks Middleware

You can customize the health checks middleware using the `WithHealthChecksMiddleware` method:

```csharp
app.UseEndatixMiddleware(middleware => middleware
    .WithHealthChecksMiddleware(options => {
        options.Path = "/system/health";
        options.ResponseWriter = WriteHealthCheckResponse;
    }));
```

## Disabling Health Checks

If you want to disable health checks entirely, you can use the `SkipHealthChecks` method:

```csharp
builder.Host.ConfigureEndatix(endatix => endatix
    .SkipHealthChecks());
```

## Health Checks UI

Endatix does not include Health Checks UI by default, but you can easily add it using the standard ASP.NET Core Health Checks UI package:

```csharp
builder.Services.AddHealthChecksUI().AddInMemoryStorage();

// In middleware configuration
app.UseHealthChecksUI(options => {
    options.UIPath = "/health-ui";
    options.ApiPath = "/health-api";
});
```

## Best Practices

- Use health checks to monitor critical dependencies
- Add custom health checks for business-critical components
- Configure appropriate failure statuses for different checks
- Use tags to categorize health checks
- Set up monitoring and alerting based on health check results 