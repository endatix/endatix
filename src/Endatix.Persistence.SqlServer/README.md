# Endatix.Persistence.SqlServer

Adds support for using Microsoft SQL Server database persistence with the Endatix Platform. This package is crucial for applications that require SQL Server as the primary database.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET. It is designed for building secure, scalable, and integrated form-centric applications that work with SurveyJS. Endatix empowers business users with advanced workflows, automation, and meaningful insights.

## Installation:

```bash
dotnet add package Endatix.Persistence.SqlServer
```

## More Information:
For detailed installation instructions, please visit [Endatix Installation Guide](https://docs.endatix.com/docs/getting-started/installation).

# Generating Migrations

>[!NOTE]
>Please note to change the name of the migration for the respective context. This KB is WIP and is subject to change as the process evolves to simplify migrations tooling and process

## For AppDbContext Entities
```bash
## For MS SQL Server
dotnet ef migrations add InitialEntities --startup-project src/Endatix.WebHost --project src/Endatix.Persistence.SqlServer  --context AppDbContext --output-dir Migrations/AppEntities
```

## For AppIdentityDbContext Entities

```bash
## For MS SQL Server
dotnet ef migrations add InitialIdentity --startup-project src/Endatix.WebHost --project src/Endatix.Persistence.SqlServer  --context AppIdentity
```



## Setup

1. Create SQL Server in Docker - `docker run --name sqlserver_db -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=admin@2admin" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2019-latest`
2. Update appSettings.Development.json by adding this connection string
```json
"DefaultConnection": "Server=localhost;Initial Catalog=endatix-db; User ID=sa; Password=admin@2admin; TrustServerCertificate=True",
"DefaultConnection_DbProvider": "SqlServer" // Config first db provider setter. Not required if you configure the DB Provider via c# code.
```