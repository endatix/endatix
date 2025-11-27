---
sidebar_position: 4
title: Setup From Repository
description: Clone and run Endatix directly from the source code
---

import Tabs from "@theme/Tabs";
import TabItem from "@theme/TabItem";

# Setup From Repository

If you want to explore the source code, contribute to the project, or customize Endatix for your specific needs, you can clone the Git repository and run it directly. This approach is recommended for developers who want to understand the internals or contribute to the project.

## Prerequisites

Before getting started, make sure you have:

- [Git](https://git-scm.com/downloads)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) or higher
- IDE: [Visual Studio Code](https://code.visualstudio.com/), [Cursor](https://www.cursor.com/), [Rider](https://www.jetbrains.com/rider/) or [Visual Studio](https://visualstudio.microsoft.com/)

## Step 1: Clone the Repository

```bash
git clone https://github.com/endatix/endatix.git
cd endatix
```

## Step 2: Build the Solution

```bash
dotnet build
```

This will restore all NuGet packages and build the entire solution.

## Step 3: Configure the Application

Cd into the src/Endatix.WebHost folder and open the appsettings.Development.json file. You need to configure it before running the application for the first time.

<details>
  <summary>Expand the code block below to make changes to **appsettings.Development.json**</summary>

:bulb: **Note on Connection Strings:** The Default connection string is required. Find the line below and update the connection string to your own. Also, if you want to use PostgresSQL as the database provider add the following line:

```json {2,3} showLineNumbers wrap
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Initial Catalog=Endatix.App;"
  "DefaultConnection_DbProvider": "postgresql" // add this line if you want to use PostgresSQL as the database provider. Otherwise you don't need it.
}
// Rest of the settings...
```

:bulb: **Note on Initial User:** Line 6 and 7 are optional and they allow you to configure the credentials for the initial user. Change the email and password to your own credentials. You can remove these lines upon successful app setup and login. Never commit your credentials to the repository.

```json {6,7} showLineNumbers
  "Endatix": {
    "Data": {
      "EnableAutoMigrations": true,
      "SeedSampleData": false,
      "InitialUser": {
        "Email": "admin@endatix.com", // change this to your own email
        "Password": "P@ssw0rd" // change this to a more secure password
      }
    }
    // Other settings...
  }
```

</details>

By default, the sample application uses MS SQL Server. If you want to use a real database, ensure you update the database provider in the connection string configuration.

## Step 4: Run the Sample Application

Navigate to the sample WebAPI project and run it:

```bash
cd src/Endatix.WebHost
dotnet run
```

The API will be available at `https://localhost:5001` by default.

## Step 5: Verify the Installation

After starting the application, you can verify that Endatix is running correctly by:

- **Verify the service is healthy** - `https://localhost:5001/health/ui`
- **View the Swagger UI** - `https://localhost:5001/api-docs`
- **Login** - use the default credentials from the appsettings.Development.json file to authenticate and change them after login

## Configuration Options

For detailed information on configuring your Endatix application, please refer to our configuration guides:

- [Configuring Endatix](/docs/configuration/) - Learn about the different configuration approaches and paradigms

## Solution Structure

Here's an overview of the key projects in the repository:

- **Endatix.Core**: Contains the core functionality and interfaces
- **Endatix.Framework**: Core framework project for customization and extensibility points
- **Endatix.Infrastructure**: Main implementations of the Core project
- **Endatix.Api**: The REST API endpoints for interacting with Endatix
- **Endatix.Persistence.SqlServer**: MS SQL Server specific database implementation
- **Endatix.Persistence.PostgreSql**: PostgreSQL specific database implementation
- **Endatix.Hosting**: Main hosting infrastructure package
- **Endatix.WebHost**: Default app host project with minimal code

[Check out the full documentation about the solution structure](/docs/getting-started/architecture#net-solution-structure)

## Development Workflow

1. Create a new branch for your feature or bug fix
2. Make your changes following the project's coding standards
3. Write or update tests for your changes
4. Submit a pull request for review

## Running Tests

To run the unit tests:

```bash
dotnet test
```

## Troubleshooting

If you encounter any issues during setup:

1. Ensure you have the correct .NET SDK version installed
2. Make sure all required packages are restored with `dotnet restore`
3. Check that the database connection string is correct if using a real database
4. Review the project documentation in the `/docs` folder for more specific information

## Next Steps

After successful setup, you can:

- Explore the [API endpoints](/docs/api)
- Use the [Swagger UI](https://localhost:5001/api-docs) to authenticate and test the API endpoints
- Learn how to create and manage forms - `🚧 coming soon`
- Understand how to handle form submissions - `🚧 coming soon`
