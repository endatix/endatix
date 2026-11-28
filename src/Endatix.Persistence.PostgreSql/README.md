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

### Upgrading existing form definitions

`NumberFormDefinitions` numbers existing definitions without changing the live definition ID. EF's migration seeder then adopts newer legacy drafts and requests Reporting schema compilation for active definitions previously marked as drafts. It uses the same C# canonical hash as the editor.

For a generated-SQL deployment with auto-migrations off:

1. Before deployment, rehearse on a representative database copy. Record the queued form and definition counts and the time to finish the whole queue. Compare that time with your request and database command timeouts; schedule a maintenance window that allows the run and, if interrupted, a full retry.
2. Apply the generated AppDbContext migration script. Draft reads and writes return HTTP 503 for queued forms. Legacy definitions remain addressable by ID, but a write to a locked row can wait for the entire queue to commit or time out.
3. Run `dotnet ef database update --context AppDbContext --project src/Endatix.Persistence.PostgreSql --startup-project src/Endatix.WebHost` from the deployed release against the same database. The EF seeder drains the queue in one transaction. It retains locks for every processed form until commit. If interrupted before commit, the whole transaction rolls back, including adoption, Reporting events and queue removals. Rerun the same command.
4. Verify the migration is present **and** the queue is empty:

   ```sql
   SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261128073201_NumberFormDefinitions'; -- expect 1
   SELECT count(*) FROM "LegacyDraftUpgrades"; -- expect 0
   ```

Expansion leaves unbound stray definitions visible by ID: an older editor may already have read one and save after adoption. At the later cutover, first drain incompatible writers, then re-read the latest content, pointer and submission bindings. Reconcile later acknowledged edits with the current draft before hiding any stray. Queue completion alone does not enable production publishing.

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

1. First list your migrations with `dotnet ef migrations list` to check if the migration you want to remove must be unapplied on your local DB

```bash
dotnet ef migrations list \
  --startup-project src/Endatix.WebHost \
  --project src/Endatix.Persistence.PostgreSql \
  --context <AppDbContext|AppIdentityDbContext>
```

2. If required update to the migration via `dotnet ef database update`

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
