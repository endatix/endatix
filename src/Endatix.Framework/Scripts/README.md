# SQL Script Infrastructure

Store SQL scripts in separate `.sql` files with native syntax highlighting and source control tracking.

## Directory Structure

```
Endatix.Persistence.PostgreSql/
├── Scripts/
│   ├── Functions/
│   │   └── export_form_submissions.sql
│   ├── Views/
│   └── Triggers/

Endatix.Persistence.SqlServer/
├── Scripts/
│   ├── Procedures/
│   │   └── export_form_submissions.sql
│   ├── Functions/
│   └── Views/
```

## Usage in Migrations

**Recommended:**
```csharp
using Microsoft.EntityFrameworkCore.Migrations;
using Endatix.Framework.Scripts;

public partial class MyMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var script = migrationBuilder.ReadEmbeddedSqlScript("Functions/my_function.sql");
        migrationBuilder.Sql(script);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS my_function;");
    }
}
```

## Setup

Both persistence projects are already configured with:
- Framework reference
- Embedded resource configuration for `Scripts\**\*.sql`

## Best Practices

- Use lowercase with underscores: `export_form_submissions.sql`
- Store database-specific scripts in appropriate persistence projects
- Prefer the `migrationBuilder.ReadEmbeddedSqlScript()` extension method

## Error Handling

When a script is not found, you'll get a detailed error with available scripts:
```
SQL script not found: Functions/missing_script.sql
Available SQL script resources:
  - Functions/export_form_submissions.sql
  - Views/submission_summary.sql
``` 