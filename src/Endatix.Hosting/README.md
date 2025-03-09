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
    builder.Services.AddEndatixWithDefaults(builder.Configuration);
    
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
builder.Services.AddEndatix(builder.Configuration)
    .Logging
        // Customize bootstrap logger
        .ConfigureBootstrapLogger(config => 
            config.MinimumLevel.Debug()
                 .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
        // Register and customize the configured logger
        .ConfigureSerilog(config => 
            config.WriteTo.File("logs/custom-.log", rollingInterval: RollingInterval.Day))
        // Add Application Insights
        .UseApplicationInsights()
    .Build();
```

The builder pattern ensures that all logging components work together seamlessly:
- `ConfigureBootstrapLogger` - Customizes the bootstrap logger before creation
- `CreateBootstrapLogger` - Creates the bootstrap logger if needed
- `ConfigureSerilog` - Customizes the fully configured logger
- `RegisterConfiguredLogger` - Sets up the configuration for the full logger
- `UseApplicationInsights` - Adds Application Insights integration

## Available Methods

### AddEndatix

The core method that adds all required Endatix services:

```csharp
// Basic registration
services.AddEndatix(configuration);

// Using the builder pattern for advanced configuration
services.AddEndatix(configuration)
    .AddApi()
    .AddDatabase<TContext>()
    .AddAuthentication()
    .Build();
```

### AddEndatixWithDefaults

A convenience method that adds Endatix with sensible defaults:

```csharp
services.AddEndatixWithDefaults(configuration);
```

### AddEndatixWithSqlServer<TContext>

A convenience method that adds Endatix with SQL Server:

```csharp
services.AddEndatixWithSqlServer<MyDbContext>(configuration);
```

### AddEndatixWithPostgreSql<TContext>

A convenience method that adds Endatix with PostgreSQL:

```csharp
services.AddEndatixWithPostgreSql<MyDbContext>(configuration);
```

## Documentation

For detailed documentation and examples, see:

- [Setup Guide](https://docs.endatix.com/docs/getting-started/setup-nuget-package)
- [Advanced Hosting Configuration](https://docs.endatix.com/docs/building-your-solution/hosting)

## License

This project is licensed under the MIT License - see the LICENSE file for details.