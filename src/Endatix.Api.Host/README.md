# Endatix.Api.Host

Install this package to host the Endatix API - the key presentation layer of the Endatix Platform. It includes both the Public and Management APIs, providing the core interface for interacting with the Endatix Platform.

> [!TIP] >**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET. It is designed for building secure, scalable, and integrated form-centric applications that work with SurveyJS. Endatix empowers business users with advanced workflows, automation, and meaningful insights.

## Installation:

```bash
dotnet add package Endatix.Api.Host
```

## More Information:

For detailed installation instructions, please visit [Endatix Installation Guide](https://docs.endatix.com/docs/getting-started/installation).

## Getting Started

### 1. Setup Program.cs with Two-Stage Logging

Following Serilog's best practices, Endatix uses a two-stage initialization approach for logging:

```csharp
using Endatix.Hosting;
using Serilog;

// Create the bootstrap logger at startup to capture early errors
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting Endatix API...");

try
{
    // Standard ASP.NET Core startup
    var builder = WebApplication.CreateBuilder(args);
    
    // Add Endatix with default configuration
    // This sets up the fully configured logger based on appsettings.json
    builder.Services.AddEndatixWithDefaults(builder.Configuration);
    
    var app = builder.Build();
    app.UseEndatix();
    app.Run();
    
    return 0;
}
catch (Exception ex)
{
    // Log any unhandled exceptions
    Log.Fatal(ex, "Endatix API terminated unexpectedly");
    return 1;
}
finally
{
    // Ensure all logs are flushed
    Log.CloseAndFlush();
}
```

### 2. Configure appsettings.json

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
          "path": "logs/endatix-api-.log",
          "rollingInterval": "Day",
          "fileSizeLimitBytes": 10485760,
          "retainedFileCountLimit": 7,
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

## How Two-Stage Logging Works

Endatix uses Serilog's recommended approach for complete logging coverage:

1. **Bootstrap Logger**:
   - Created immediately when the program starts
   - Captures any errors during startup before the host is built
   - Simple console logger for early diagnostics

2. **Fully Configured Logger**:
   - Created once the host is built and configuration is loaded
   - Uses all the settings from your appsettings.json
   - Automatically replaces the bootstrap logger
   - Provides rich logging across your entire application

## Advanced Logging Configuration

You can customize logging beyond the default setup using the fluent builder API:

```csharp
// Customize the bootstrap logger
builder.Services.AddEndatix(builder.Configuration)
    .Logging
        .ConfigureBootstrapLogger(config => 
            config.MinimumLevel.Debug()
                 .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .Build();

// Add additional log sinks
builder.Services.AddEndatix(builder.Configuration)
    .Logging
        .ConfigureSerilog(config => 
            config.WriteTo.Seq("http://localhost:5341"))
    .Build();

// Add Application Insights integration
builder.Services.AddEndatix(builder.Configuration)
    .Logging
        .UseApplicationInsights()
    .Build();
```

This builder pattern gives you full control over your logging configuration while maintaining the benefits of the two-stage approach.

## Benefits of Two-Stage Logging

1. **Complete Coverage**: Captures all events from the very first line of your application
2. **Early Error Detection**: Ensures startup errors are logged, even before configuration is loaded
3. **Configuration Freedom**: Use your appsettings.json to configure logging as needed
4. **Simplified Integration**: Just add two blocks of code to your Program.cs
