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

## Migrations Guide

Run the commands from the `oss` folder.

> [!NOTE]
> Always use `Endatix.WebHost` as startup project, and this provider-specific persistence project as migrations project.

### Add migration

#### App entities (`AppDbContext`)
```bash
dotnet ef migrations add <MigrationName> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.SqlServer \
  --context AppDbContext \
  --output-dir Migrations/AppEntities
```

#### Identity entities (`AppIdentityDbContext`)
```bash
dotnet ef migrations add <MigrationName> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.SqlServer \
  --context AppIdentityDbContext \
  --output-dir Migrations/AppIdentity
```

### Remove last migration

1. First list your migraitons with `dotnet ef migrations list` to check if the migration you want to remove must be unapplied on your local DB

```bash
dotnet ef migrations list \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.SqlServer \
  --context <AppDbContext|AppIdentityDbContext>
```

2. If required update to the migraiton via `dotnet ef database update`

```bash
dotnet ef database update <PreviousMigrationName> \       
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.SqlServer \
  --context <AppDbContext|AppIdentityDbContext> 
```

3. Remove the last migration

```bash
dotnet ef migrations remove \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.SqlServer \
  --context <AppDbContext|AppIdentityDbContext> 
```


### Apply migrations

#### App entities (`AppDbContext`)
```bash
dotnet ef database update \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.SqlServer \
  --context AppDbContext
```

#### Identity entities (`AppIdentityDbContext`)
```bash
dotnet ef database update \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.SqlServer \
  --context AppIdentityDbContext
```



## Setup

1. Create SQL Server in Docker - `docker run --name sqlserver_db -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=admin@2admin" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2019-latest`
2. Update appSettings.Development.json by adding this connection string
```json
"DefaultConnection": "Server=localhost;Initial Catalog=endatix-db; User ID=sa; Password=admin@2admin; TrustServerCertificate=True",
"DefaultConnection_DbProvider": "SqlServer" // Config first db provider setter. Not required if you configure the DB Provider via c# code.
```