# Endatix.Hosting

Install this package to add the required hosting infrastructure for Endatix Platform.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET. It is designed for building secure, scalable, and integrated form-centric applications that work with SurveyJS. Endatix empowers business users with advanced workflows, automation, and meaningful insights.

## Installation:

```bash
dotnet add package Endatix.Hosting
```

## More Information:
For detailed installation instructions, please visit [Endatix Installation Guide](https://docs.endatix.com/docs/getting-started/installation).

## Getting Started

### 1. Add Endatix to Your ASP.NET Core Application

```csharp
using Endatix.Hosting;
using Serilog;

// Create the bootstrap logger at startup to capture early errors
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting up...");

try
{
    // Create the web application builder
    var builder = WebApplication.CreateBuilder(args);
    
    // Add Endatix with default configuration
    // This automatically sets up the fully configured logger
    builder.Host.ConfigureEndatix();
    
    // Build and configure the application
    var app = builder.Build();
    app.UseEndatix();
    
    // Run the application
    app.Run();
    return 0;
}
catch (Exception ex)
{
    // Log startup errors
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    // Ensure logs are properly flushed
    Log.CloseAndFlush();
}
```

### 2. Configure Logging in appsettings.json

Add Serilog configuration to your appsettings.json file:

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Sixteen"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/app-.log",
          "rollingInterval": "Day",
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

### 3. Customizing Swagger Documentation

Endatix provides several ways to customize Swagger documentation:

```csharp
// Using app.UseEndatix with options
app.UseEndatix(options =>
{
    // Basic toggle for enabling/disabling Swagger
    options.UseSwagger = true;
    options.EnableSwaggerInProduction = false;
    
    // Custom path for Swagger UI (default is "/swagger")
    options.SwaggerPath = "/api-docs";
    
    // Advanced Swagger configuration
    options.ConfigureOpenApiDocument = settings => 
    {
        settings.DocumentName = "v1";
        settings.PostProcess = (document, _) => 
        {
            document.Info.Title = "My Custom API";
            document.Info.Version = "1.0";
            document.Info.Description = "API documentation for my application";
        };
    };
    
    // Customize Swagger UI appearance
    options.ConfigureSwaggerUi = settings => 
    {
        settings.DocExpansion = "list";
        settings.DefaultModelsExpandDepth = 1;
        settings.OAuth2Client = new NSwag.AspNetCore.OAuth2ClientSettings
        {
            ClientId = "api-client-id",
            AppName = "My API Client"
        };
    };
});
```

You can also configure Swagger directly using the middleware builder pattern:

```csharp
// Using the builder pattern for fine-grained control
app.UseEndatix()
    .UseSwagger("/api-docs", 
        openApiSettings => 
        {
            openApiSettings.DocumentName = "v1";
        },
        swaggerUiSettings => 
        {
            swaggerUiSettings.DocExpansion = "list";
        });
```

## Two-Stage Logging

Endatix follows Serilog's recommended two-stage initialization approach:

1. **Bootstrap Logger**: 
   - Created immediately when the program starts
   - Captures any errors during startup before the host is built
   - Uses a simple console sink

2. **Fully Configured Logger**:
   - Created once the host is built and configuration is loaded
   - Configured based on your appsettings.json
   - Automatically replaces the bootstrap logger
   - Provides rich logging capabilities across your application

## Advanced Logging Configuration

Endatix provides a clean builder API for configuring logging that follows Serilog's best practices:

```csharp
// Full control over bootstrap and configured loggers
builder.Host.ConfigureEndatix(endatix => endatix
    .WithLogging(logging => logging
        // Customize bootstrap logger
        .ConfigureBootstrapLogger(config => 
            config.MinimumLevel.Debug()
                 .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
        // Register and customize the configured logger
        .ConfigureSerilog(config => 
            config.WriteTo.File("logs/custom-.log", rollingInterval: RollingInterval.Day))
        // Add Application Insights
        .UseApplicationInsights()));
```

The builder pattern ensures that all logging components work together seamlessly:
- `ConfigureBootstrapLogger` - Customizes the bootstrap logger before creation
- `CreateBootstrapLogger` - Creates the bootstrap logger if needed
- `ConfigureSerilog` - Customizes the fully configured logger
- `RegisterConfiguredLogger` - Sets up the configuration for the full logger
- `UseApplicationInsights` - Adds Application Insights integration

## Available Methods

### ConfigureEndatix

The core method that configures Endatix on the host:

```csharp
// Basic configuration with defaults
builder.Host.ConfigureEndatix();

// Using the builder pattern for advanced configuration
builder.Host.ConfigureEndatix(endatix => endatix
    .WithApi(api => api
        .AddSwagger()
        .AddVersioning())
    .WithPersistence(db => db
        .UseSqlServer<AppDbContext>()
        .EnableAutoMigrations())
    .WithSecurity(security => security
        .UseJwtAuthentication()));
```

## Documentation

For detailed documentation and examples, see:

- [Setup Guide](https://docs.endatix.com/docs/getting-started/setup-nuget-package)
- [Advanced Hosting Configuration](https://docs.endatix.com/docs/building-your-solution/hosting)

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Database Migrations and Data Seeding

Endatix provides flexible options for handling database migrations and data seeding:

### Default Behavior

When using the default configuration with `AddEndatixWithDefaults` or calling `UseDefaults()` on the persistence builder, Endatix will:

1. Configure database persistence based on your settings
2. Enable automatic database migrations at startup
3. Enable sample data seeding at startup

```csharp
// Default configuration that includes auto migrations and data seeding
builder.Services.AddEndatixWithDefaults(builder.Configuration);
```

### Custom Configuration

If you want more control, you can enable or disable these features individually:

```csharp
// Manual configuration
builder.Services.AddEndatix(builder.Configuration)
    .Persistence
        // Enable automatic database migrations (enabled by default with UseDefaults)
        .EnableAutoMigrations(true)
        // Enable sample data seeding (enabled by default with UseDefaults)
        .EnableSampleDataSeeding(true)
    .Build();
```

### Manual Migration and Seeding

For even more control, you can apply migrations and seed data explicitly:

```csharp
// In Program.cs
var app = builder.Build();

// Configure middleware
app.UseEndatix();

// Apply migrations and seed data explicitly in development
if (app.Environment.IsDevelopment())
{
    await app.ApplyDbMigrationsAsync();
    await app.SeedInitialUserAsync();
}
```

### Configuration Settings

You can control migrations and data seeding through configuration:

```json
{
  "Endatix": {
    "Data": {
      "EnableAutoMigrations": true,
      "SeedSampleData": true,
      "InitialUser": {
        "Email": "admin@example.com",
        "Password": "SecurePassword123!"
      }
    }
  }
}
```

> **Note**: Configuration values in `appsettings.json` take precedence over code defaults. If you explicitly set `EnableAutoMigrations` or `SeedSampleData` in your configuration file, those values will be used regardless of what you specify in code.

## Configuration Architecture

### Options Ownership

In Endatix, each builder is the sole owner of its domain-specific options:

- **EndatixPersistenceBuilder**: Owns and configures `DataOptions` (database migrations, data seeding)
- **EndatixSecurityBuilder**: Owns and configures `SecurityOptions`
- **EndatixApiBuilder**: Owns and configures API-specific options

### Configuration Precedence

For each option, the following precedence is applied:

1. **appsettings.json values** - Always take highest precedence when specified
2. **Code defaults** - Applied when configuration values are not present
3. **Class defaults** - Used when neither of the above specifies a value

### Example

```csharp
// Configuration in appsettings.json takes precedence over code:
{
  "Endatix": {
    "Data": {
      "EnableAutoMigrations": false  // This will override any code value
    }
  }
}

// Code defaults applied if not in configuration:
builder.Persistence.EnableAutoMigrations(true);

// DataOptions class default (used if neither of above is set):
public bool EnableAutoMigrations { get; set; } = false;
```