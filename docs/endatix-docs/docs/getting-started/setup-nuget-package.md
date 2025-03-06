---
sidebar_position: 3
---

# Setup Using NuGet Package

This guide will help you quickly set up Endatix Platform using NuGet package manager, providing details about what code and configurations are needed to get it running.

## Prerequisites

- .NET 9.0

## Step-by-Step Installation

:::note

The following process is done via dotnet package manager and has manual steps involved, which is prone to errors and tedious. It serves the purpose of getting started.

We are working on ways to improve the developer experience with more interactive UI and features available to developers during the setup. More info to come on this page as Installation options improve.

:::

### Step 1: Create a new .NET project
run the command:
`dotnet new web -n endatix-platform`

and then cd into endatix-platform

### Step 2: Open the project in your IDE

If you use VS Code, you can enter `code .`

### Step 3: Install the Endatix packages

You have two options for setting up Endatix:

#### Option A: Using Endatix.Api.Host (Simplified)

Run `dotnet add package Endatix.Api.Host --prerelease` or use NuGet package manager.

This package provides a simplified setup experience with minimal configuration.

#### Option B: Using Endatix.Hosting (Recommended)

Run `dotnet add package Endatix.Hosting --prerelease` or use NuGet package manager.

This package provides a more flexible configuration experience using the builder pattern, which gives you fine-grained control over all aspects of your application.

### Step 4: Edit Program.cs

#### Option A: Using Endatix.Api.Host

```csharp
using Endatix.Setup;

var builder = WebApplication.CreateBuilder(args);

builder.CreateEndatix()
    .AddDefaultSetup()
    .AddApiEndpoints();

var app = builder.Build();

app.UseEndatixMiddleware()
    .UseEndatixApi();

app.Run();
```

#### Option B: Using Endatix.Hosting

```csharp
using Endatix.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add Endatix with default configuration
builder.Services.AddEndatixWithDefaults(builder.Configuration);

// Or for more control, use the builder pattern:
/*
builder.Services.AddEndatix(builder.Configuration)
    .Api.AddSwagger().Build()
    .Security.UseJwtAuthentication().Build()
    .UseSqlServer<AppDbContext>()
    .EnableAutoMigrations();
*/

var app = builder.Build();

// Configure Endatix middleware
app.UseEndatix();
app.Run();
```

### Step 5: Configure the AppSettings

Endatix uses Serilog with settings from the config. Copy the config and paste it in your `appsettings.json` or `appsettings.Development.json` where you shall configure your settings. Mind the following:
* `ConnectionStrings:DefaultConnection` - add your connection to MS SqlServer here
* `Security:JwtSigningKey` - generate random JWT signing key via a string. You can type `openssl genrsa 512` in your terminal to generate a random key
* `Security:DevUsers` - add email and password for your dev user, which will be used for authentication

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "{YOUR_CONNECTION_STRING_HERE}"
  },
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "System": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "applyThemeToRedirectedOutput": true,
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Sixteen, Serilog.Sinks.Console",
          "outputTemplate": "[{Timestamp: HH:mm:ss.fff} Level:{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "/logs/log-.txt",
          "rollingInterval": "Day",
          "rollOnFileSizeLimit": true,
          "formatter": "Serilog.Formatting.Json.JsonFormatter"
        }
      }
    ],
    "Enrich": [
      "FromLogContext",
      "WithMachineName",
      "WithProcessId",
      "WithThreadId"
    ],
    "Properties": {
      "Application": "Endatix Local Development",
      "Environment": "Local Development"
    }
  },
  "Security": {
    "JwtSigningKey": "{YOUR_JWT_SIGNING_KEY_HERE}",
    "JwtExpiryInMinutes": 1440,
    "DevUsers": [
      {
        "Email": "{USER_EMAIL}",
        "Password": "{USER_PASSWORD}",
        "Roles": ["Admin", "Manager"]
      }
    ]
  }
}
```

### Step 6: Run the application

Run the application using the following command:

```bash
dotnet run
```

The application should now be running on `https://localhost:7066` (or a similar port). You can access the Swagger UI at `https://localhost:7066/swagger`.

## What's Included in the Default Setup

When you use `AddEndatixWithDefaults()`, the following features are automatically configured:

- **API endpoints** with Swagger documentation
- **JWT authentication** with secure defaults
- **Standard authorization policies**
- **Database persistence** (detected from connection string)
- **Logging** with Serilog

## Next Steps

For more advanced configuration options and detailed usage of the Endatix.Hosting package, check out:

- [Hosting Configuration](/docs/building-your-solution/hosting) - Learn how to use the builder pattern for advanced configuration
- [Authentication](/docs/building-your-solution/authentication) - Configure authentication and authorization