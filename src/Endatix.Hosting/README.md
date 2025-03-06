# Endatix.Hosting

Endatix.Hosting provides a clean, fluent API for configuring and extending your Endatix applications using the builder pattern. This library is the main entry point for integrating Endatix into your ASP.NET Core applications.

## Overview

The Endatix.Hosting package offers:

- A fluent builder pattern for configuring all aspects of your application
- Simplified service registration with sensible defaults
- Modular configuration for API, security, persistence, and logging
- Extensibility points for customizing behavior

## Key Components

- **EndatixBuilder**: The main entry point and orchestrator
- **EndatixApiBuilder**: Configure API-related services (Swagger, versioning, CORS)
- **EndatixSecurityBuilder**: Configure authentication and authorization
- **EndatixPersistenceBuilder**: Configure database providers and options
- **EndatixLoggingBuilder**: Configure logging providers and levels
- **InfrastructureBuilder**: Configure lower-level infrastructure services

## Basic Usage

```csharp
// Add Endatix with default configuration
builder.Services.AddEndatixWithDefaults(builder.Configuration);

// Or for more control, use the builder pattern:
builder.Services.AddEndatix(builder.Configuration)
    .Api.AddSwagger().Build()
    .Security.UseJwtAuthentication().Build()
    .UseSqlServer<AppDbContext>()
    .EnableAutoMigrations();

// Configure middleware
app.UseEndatix();
```

## Documentation

For detailed documentation and examples, see:

- [Setup Guide](https://docs.endatix.com/docs/getting-started/setup-nuget-package)
- [Advanced Hosting Configuration](https://docs.endatix.com/docs/building-your-solution/hosting)

## Dependencies

This package depends on:

- Endatix.Framework
- Endatix.Infrastructure
- Endatix.Api
- Endatix.Persistence.SqlServer
- Endatix.Persistence.PostgreSql

## License

This project is licensed under the MIT License - see the LICENSE file for details.
