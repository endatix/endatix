# Endatix.Persistence.PostgreSql

Adds support for using PostgreSQL database persistence with the Endatix Platform. This package is crucial for applications that require PostgreSQL as the primary database.

>[!TIP]
>**[Endatix Platform](https://github.com/endatix/endatix)** is an open-source data collection and management library for .NET. It is designed for building secure, scalable, and integrated form-centric applications that work with SurveyJS. Endatix empowers business users with advanced workflows, automation, and meaningful insights.

## Installation:

```bash
dotnet add package Endatix.Persistence.PostgreSql
```

## Recommended Usage:

For running and hosting the Endatix Platform, **Endatix.Api.Host** is the recommended main package as it simplifies the installation and setup process.

```bash
dotnet add package Endatix.Api.Host
```

## More Information:
For detailed installation instructions, please visit [Endatix Installation Guide](https://docs.endatix.com/docs/getting-started/installation).

# Generating Migrations

>[!NOTE]
>Please note to change the name of the migration for the respective context. This KB is WIP and is subject to change as the process evolves to simplify migrations tooling and process

## For AppDbContext Entities
```bash
dotnet ef migrations add InitialEntities --startup-project src/Endatix.WebHost --project src/Endatix.Persistence.PostgreSql  --context AppDbContext --output-dir Migrations/AppEntities
```

## For AppIdentityDbContext Entities

```bash
dotnet ef migrations add InitialIdentity --startup-project src/Endatix.WebHost --project src/Endatix.Persistence.PostgreSql  --context AppIdentityDbContext --output-dir Migrations/AppIdentity
```



## Setup

1. Create Postgres in Docker - `docker run --name postgres_db -e POSTGRES_PASSWORD=[YOUR_MASTER_PASSWORD] -p 5432:5432 -d postgres`
2. Install pgAdmin [link here](https://www.pgadmin.org/). You can also add with Docker
3. Create Postgres Login using the pgAdmin
4. Update appSettings.Development.json by adding this connection string
```json
"DefaultConnection": "Host=localhost; Database=endatix-db; Username=[YOUR_PG_LOGIN]; Password=[YOUR_PG_PASSWORD]"
```