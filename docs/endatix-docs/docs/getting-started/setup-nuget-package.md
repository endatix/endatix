---
sidebar_position: 2
title: "Setup Using NuGet Package"
description: "Learn how to quickly add Endatix to your application using NuGet"
---

# Setup Using NuGet Package

This guide provides step-by-step instructions for setting up the Endatix platform using the NuGet package manager.

## Prerequisites

Before getting started, make sure you have:

- **.NET 8.0 SDK** or newer installed
- A **.NET Web API project** created with ASP.NET Core
- **NuGet Package Manager** (included with Visual Studio or the .NET CLI)

## Installing the Package

### Using the NuGet Package Manager Console

```powershell
Install-Package Endatix.Hosting
```

### Using the .NET CLI

```bash
dotnet add package Endatix.Hosting
```

## Basic Configuration

Once the package is installed, open your `Program.cs` file and update it as follows:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Endatix.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add Endatix with default configuration
builder.Host.ConfigureEndatix();

var app = builder.Build();

// Use Endatix middleware
app.UseEndatix();

app.Run();
```

This minimal setup configures Endatix with sensible defaults for most scenarios.

## Advanced Configuration Options

### Custom Configuration

For more control, you can customize specific components:

```csharp
builder.Host.ConfigureEndatix(endatix => {
    // Add your custom configuration here
    // Examples will be added as more components are fully tested and available
});
```

### Hybrid Approach

You can also start with defaults and customize only what you need:

```csharp
builder.Host.ConfigureEndatixWithDefaults(endatix => {
    // Customize specific components after applying defaults
});
```

## Environment-Specific Configuration

You can apply different configurations based on the environment:

```csharp
if (builder.Environment.IsDevelopment())
{
    // Development-specific configuration
    builder.Host.ConfigureEndatix(endatix => {
        // Development configuration
    });
}
else
{
    // Production-specific configuration
    builder.Host.ConfigureEndatix(endatix => {
        // Production configuration
    });
}
```

### AppSettings.json Configuration

Configure environment-specific settings in your `appsettings.json` file:

```json
{
  "Endatix": {
    "ApplicationName": "My Endatix App",
    "ConnectionStrings": {
      "DefaultConnection": "Server=localhost;Database=MyDatabase;Trusted_Connection=True;"
    }
  }
}
```

And for environment-specific settings, use `appsettings.{Environment}.json`:

```json
{
  "Endatix": {
    "ConnectionStrings": {
      "DefaultConnection": "Server=production-server;Database=ProductionDb;Trusted_Connection=True;"
    }
  }
}
```

## What's Included in the Default Setup

When you use `builder.Host.ConfigureEndatix()`, the following features are automatically configured with sensible defaults:

- **API endpoints**
- **Authentication and authorization**
- **Database persistence**
- **Logging**

The specific implementation details of these components are subject to change as the framework evolves.

## Next Steps

As Endatix continues to develop, future versions will provide more detailed configuration options for each component. For now, the recommended approach is to use the default configuration unless you have specific requirements that need customization.

Refer to the [Advanced Hosting Configuration](/docs/building-your-solution/hosting) guide for more information.