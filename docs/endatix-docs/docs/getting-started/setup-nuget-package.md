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

### Step 3: Install the Endatix.Api
run `dotnet add package Endatix.Api.Host  --prerelease` or use NuGet package manager

### Step 4: Edit Program.cs

Open `Program.cs` file. Add the following code after the builder initialization:

```csharp
builder.CreateEndatix()
    .AddDefaultSetup()
    .AddApiEndpoints();
```

Find where where the app is defined (`var app = builder.Build();`) and below it add the following code, which will register the Endatix middleware:

```csharp
app.UseEndatixMiddleware()
            .UseEndatixApi();
```

At the end your Program.cs should look something like this.

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

### Step 5: Configure the AppSettings

Endatix uses Serilog with settings from the config. Copy the config and paste it in your `appsettings.json` or `appsettings.Development.json` where you shall configure your settings. Mind the following:
* `ConnectionStrings:DefaultConnection` - add your connection to MS SqlServer here
* `Security:JwtSigningKey` - generate random JWT signing key via a string. You can type `openssl genrsa 512` in your terminal to generate a random key
* `Security:DevUsers` - add email and password for your dev user, which will be used for authentication

```json
{
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