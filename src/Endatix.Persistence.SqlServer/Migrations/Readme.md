# Generating Migrations

>[!NOTE]
>Please note to change the name of the migration for the respective context. This KB is WIP and is subject to change as the process evolves to simplify migrations tooling and process

## For AppDbContext Entities
```bash
dotnet ef migrations add InitialEntities --startup-project src/Endatix.WebHost --project src/Endatix.Persistence.SqlServer  --context AppDbContext --output-dir Migrations/AppEntities
```

## For AppIdentityDbContext Entities

```bash
dotnet ef migrations add InitialEntities --startup-project src/Endatix.WebHost --project src/Endatix.Persistence.SqlServer  --context AppIdentityDbContext --output-dir Migrations/AppEntities
```