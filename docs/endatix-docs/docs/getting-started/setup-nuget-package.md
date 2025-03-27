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

```csharp
using Endatix.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Option 1: Apply sensible defaults (simplest approach)
builder.Host.ConfigureEndatix();

// Option 2: Custom configuration (full control)
/*
builder.Host.ConfigureEndatix(endatix => {
    // Configure only the components you need
    endatix.WithApi(api => api
        .AddSwagger(options => {
            options.Title = "My API";
            options.Version = "v1";
        })
        .AddVersioning());
        
    endatix.WithPersistence(db => db
        .UseSqlServer<AppDbContext>(options => {
            options.ConnectionString = "Server=myServer;Database=myDb;";
        }));
        
    endatix.WithSecurity(security => security
        .UseJwtAuthentication());
        
    endatix.WithLogging(logging => logging
        .ConfigureSerilog(config => {
            config.MinimumLevel.Information();
        }));
});
*/

// Option 3: Hybrid approach - defaults plus customization
/*
builder.Host.ConfigureEndatixWithDefaults(endatix => {
    // Apply defaults first, then customize specific parts
    endatix.WithApi(api => api
        .AddSwagger(options => {
            options.Title = "My Custom API";
        }));
        
    // Override default settings where needed
    endatix.WithPersistence(db => db
        .EnableAutoMigrations());
});
*/

var app = builder.Build();

// Configure Endatix middleware
app.UseEndatix();
app.Run();
```

In the above examples:

- **Option 1**: Applies all sensible defaults with minimal code - perfect for getting started quickly
- **Option 2**: Gives you complete control over what gets configured - ideal for advanced scenarios
- **Option 3**: Starts with defaults and lets you selectively override specific settings - best of both worlds

Choose the approach that best fits your needs.

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
  "Endatix": {
    "Data": {
      "EnableAutoMigrations": true,
      "SeedSampleData": true,
      "InitialUser": {
        "Email": "{USER_EMAIL}",
        "Password": "{USER_PASSWORD}",
        "FirstName": "Admin",
        "LastName": "User"
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
    },
    "Api": {
      "UseSwagger": true,
      "SwaggerPath": "/swagger"
    }
  }
}
```

> **Note**: For API configuration, only `UseSwagger` and `SwaggerPath` can be configured through appsettings.json. Other API options like `RoutePrefix` and `VersioningPrefix` must be configured programmatically in your application code.

### Step 6: Environment-Specific Configuration

When deploying to different environments (Development, CI, Production), you can use environment-specific appsettings files to configure different behaviors without duplicating configuration:

#### Base Configuration in appsettings.json

```json
{
  "Endatix": {
    "Api": {
      "SwaggerPath": "/api-docs"
    }
  }
}
```

#### Development Environment (appsettings.Development.json)

```json
{
  "Endatix": {
    "Api": {
      "UseSwagger": true
    }
  }
}
```

#### Production Environment (appsettings.Production.json)

```json
{
  "Endatix": {
    "Api": {
      "UseSwagger": false
    }
  }
}
```

This approach follows the DRY principle (Don't Repeat Yourself) by only specifying what changes between environments.

In your GitHub Actions workflows, you can set the environment by setting the `ASPNETCORE_ENVIRONMENT` environment variable:

```yaml
- name: Deploy to Production
  env:
    ASPNETCORE_ENVIRONMENT: Production
  run: dotnet publish -c Release
```

### Step 7: Customize Swagger Configuration (Optional)

If you need to customize Swagger beyond the basic settings in `appsettings.json`, you can configure it in your `Program.cs` file:

```csharp
// Advanced Swagger customization
builder.Host.UseEndatix(endatix => endatix
    .WithApi(api => api
        .UseDefaults()
        .AddSwagger(options =>
        {
            options.IncludeXmlComments = true;
            options.TagsFromNamespaceStrategy = true;
        })));

var app = builder.Build();

// Configure the middleware with custom Swagger settings
app.UseEndatix(options => 
{
    // Configure API options
    options.ApiOptions.SwaggerPath = "/api-docs";
    options.ApiOptions.UseSwagger = builder.Environment.IsDevelopment();
    
    // Advanced OpenAPI document configuration
    options.ApiOptions.ConfigureOpenApiDocument = settings => 
    {
        settings.DocumentName = "v1";
        settings.PostProcess = (document, _) => 
        {
            document.Info.Title = "My API";
            document.Info.Version = "1.0";
            document.Info.Description = "API documentation";
        };
    };
    
    // Note: RoutePrefix and VersioningPrefix must be configured programmatically
    // as they're not available via appsettings.json
    options.ApiOptions.RoutePrefix = "api"; // Default is "api"
    options.ApiOptions.VersioningPrefix = "v"; // Default is "v"
});
```

For more advanced scenarios, you can also use the middleware builder pattern directly:

```csharp
// Get environment from configuration
var isDevEnvironment = app.Environment.IsDevelopment();

app.UseEndatix()
    .UseApi(options => {
        // Configurable via appsettings.json
        options.UseSwagger = isDevEnvironment; // Conditionally enable Swagger
        options.SwaggerPath = "/api-docs";
        
        // Must be configured programmatically (not available via appsettings.json)
        options.RoutePrefix = "api";
        options.VersioningPrefix = "v";
    });
```

### Step 8: Run the application

Run the application using the following command:

```bash
dotnet run
```

The application should now be running on `https://localhost:7066` (or a similar port). You can access the Swagger UI at `https://localhost:7066/swagger` or the path you configured.

## What's Included in the Default Setup

When you use `builder.Host.ConfigureEndatix()`, the following features are automatically configured:

- **API endpoints** with Swagger documentation
- **JWT authentication** with secure defaults
- **Standard authorization policies**
- **Database persistence** (detected from connection string)
- **Automatic database migrations** (if enabled in configuration)
- **Sample data seeding** (if enabled in configuration)
- **Logging** with Serilog
- **Health checks** with standard endpoints at `/health`, `/health/detail` (JSON), and `/health/ui` (HTML UI)

## Next Steps

For more advanced configuration options and detailed usage of the Endatix.Hosting package, check out:

- [Hosting Configuration](/docs/building-your-solution/hosting) - Learn how to use the builder pattern for advanced configuration
- [Authentication](/docs/building-your-solution/authentication) - Configure authentication and authorization