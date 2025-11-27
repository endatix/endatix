---
sidebar_position: 1
title: Quick Start
description: Get up and running with Endatix in minutes
---

# Quick Start

Endatix is an open-source backend for SurveyJS projects that can be integrated into any .NET Core project or set up in a container as a standalone application. This guide will help you install Endatix and run a simple example.

## Prerequisites

- **.NET 10.0 and above** (for NuGet & Git Repository options)
- **Docker** (for Docker Container option)

## Choose Your Installation Method

Choose the installation method that best fits your needs:

| Method                                | Best For                                           | Requirements  | Time to Setup |
| ------------------------------------- | -------------------------------------------------- | ------------- | ------------- |
| [NuGet Package ->](#install-via-git-repository)       | .NET developers integrating with existing projects | .NET 10.0+     | ~5-10 min         |
| [Docker Container ->](#install-via-docker-container) | Standalone deployment or non-.NET stacks           | Docker        | ~5 min         |
| [Git Repository ->](#install-via-git-repository)     | Contributors or customization                      | Git, .NET SDK | ~5 min        |

### Install via NuGet Package

Add the Endatix NuGet package to your .NET Core project:

```bash
dotnet add package Endatix.Hosting
```

Modify your `Program.cs` or `Startup.cs` to add Endatix and register the Endatix middleware:

```csharp
using Endatix.Hosting;

builder.Host.ConfigureEndatix();

var app = builder.Build();

app.UseEndatix();
```

[Follow the detailed NuGet setup guide →](/docs/getting-started/setup-nuget-package)

### Install via Docker Container

:::warning
**Important:** The Docker image is currently outdated and does not include the latest features available in the NuGet packages. This section will be removed once Docker images are updated to match the latest NuGet releases.
:::

[Follow the detailed Docker setup guide →](/docs/guides/docker-setup)

### Install via Git Repository

Clone the repository and run the project:

```bash
git clone https://github.com/endatix/endatix.git
cd endatix
dotnet build
cd src/Endatix.WebHost
dotnet run
```

:::info Configuration
Modify the `appsettings.json` file to configure the database connection string and other settings.
:::

The API will be available at `https://localhost:5001`.

[Follow the Git repository setup guide →](/docs/getting-started/setup-repository)

## What's Next?

Now that you have Endatix installed, here's what you can do next:

- Create your first SurveyJS form - `🚧 coming soon`
- Add your own custom event handlers - `🚧 coming soon`
- Configure webhooks - `🚧 coming soon`

Need more information about Endatix before proceeding? Check out [What is Endatix?](/docs/getting-started/what-is-endatix)
