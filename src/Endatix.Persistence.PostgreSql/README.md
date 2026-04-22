# Endatix.Persistence.PostgreSql

Adds support for using PostgreSQL database persistence with the Endatix Platform. This package is crucial for applications that require PostgreSQL as the primary database.

> [!TIP]
> **[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET. It is designed for building secure, scalable, and integrated form-centric applications that work with SurveyJS. Endatix empowers business users with advanced workflows, automation, and meaningful insights.

## Installation:

```bash
dotnet add package Endatix.Persistence.PostgreSql
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
  --project src/Endatix.Persistence.PostgreSql \
  --context AppDbContext \
  --output-dir Migrations/AppEntities
```

#### Identity entities (`AppIdentityDbContext`)

```bash
dotnet ef migrations add <MigrationName> \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.PostgreSql \
  --context AppIdentityDbContext \
  --output-dir Migrations/AppIdentity
```

### Remove last migration

1. First list your migraitons with `dotnet ef migrations list` to check if the migration you want to remove must be unapplied on your local DB

```bash
dotnet ef migrations list \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.PostgreSql \
  --context <AppDbContext|AppIdentityDbContext>
```

2. If required update to the migraiton via `dotnet ef database update`

```bash
dotnet ef database update <PreviousMigrationName> \       
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.PostgreSql \
  --context <AppDbContext|AppIdentityDbContext> 
```

3. Remove the last migration

```bash
dotnet ef migrations remove \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.PostgreSql \
  --context <AppDbContext|AppIdentityDbContext> 
```

### Apply migrations

#### App entities (`AppDbContext`)

```bash
dotnet ef database update \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.PostgreSql \
  --context AppDbContext
```

#### Identity entities (`AppIdentityDbContext`)

```bash
dotnet ef database update \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.PostgreSql \
  --context AppIdentityDbContext
```

## Local PostgreSQL Setup

1. Start PostgreSQL in Docker:

```bash
docker run --name postgres_db -e POSTGRES_PASSWORD=<MASTER_PASSWORD> -p 5432:5432 -d postgres
```

2. Create a PostgreSQL login/user and database (for example via pgAdmin).
3. Update `src/Endatix.WebHost/appsettings.Development.json`:

```json
"DefaultConnection": "Host=localhost;Database=endatix-db;Username=<PG_LOGIN>;Password=<PG_PASSWORD>",
"DefaultConnection_DbProvider": "PostgreSql"
```
