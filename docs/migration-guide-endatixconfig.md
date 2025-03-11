# Migration Guide: Removing EndatixConfig

## Overview

`EndatixConfig` is a legacy configuration approach that is being phased out in favor of the more modern options pattern using `IOptions<T>` and builder methods. This guide outlines the steps to migrate from `EndatixConfig` to the new approach.

## Why Migrate?

- **Modern Configuration**: The options pattern is the recommended approach in ASP.NET Core
- **Testability**: Easier to test and mock with dependency injection
- **Type Safety**: Strongly-typed configuration with validation
- **Integration**: Better integration with the ASP.NET Core configuration system
- **JSON Configuration**: Configuration can be set in appsettings.json files

## Migration Steps by Property

### SeedSampleData

**From**:
```csharp
EndatixConfig.Configuration.SeedSampleData = true;
// or
config.WithSampleData();
```

**To**:
```csharp
// In your startup configuration
builder.Services.AddEndatix(configuration)
    .Persistence
        .EnableSampleDataSeeding()
    .Build();

// Or in appsettings.json
{
  "Endatix": {
    "Data": {
      "SeedSampleData": true
    }
  }
}
```

### UseSnowflakeIds and SnowflakeGeneratorId

**From**:
```csharp
EndatixConfig.Configuration.UseSnowflakeIds = true;
EndatixConfig.Configuration.SnowflakeGeneratorId = 1;
// or 
config.WithSnowflakeIds(1);
```

**To**:
```csharp
// For SQL Server
builder.Services.AddEndatix(configuration)
    .Persistence
        .UseSqlServer<AppDbContext>(options => options.WithSnowflakeIds(1))
    .Build();

// For PostgreSQL
builder.Services.AddEndatix(configuration)
    .Persistence
        .UsePostgreSql<AppDbContext>(options => options.WithSnowflakeIds(1))
    .Build();
```

### TablePrefix

**From**:
```csharp
EndatixConfig.Configuration.TablePrefix = "end_";
// or
config.WithCustomTablePrefix("end_");
```

**To**:
```csharp
// For SQL Server
builder.Services.AddEndatix(configuration)
    .Persistence
        .UseSqlServer<AppDbContext>(options => options.WithCustomTablePrefix("end_"))
    .Build();

// For PostgreSQL
builder.Services.AddEndatix(configuration)
    .Persistence
        .UsePostgreSql<AppDbContext>(options => options.WithCustomTablePrefix("end_"))
    .Build();
```

### ConnectionString and MigrationsAssembly

**From**:
```csharp
EndatixConfig.Configuration.ConnectionString = "Server=.;Database=Endatix;Trusted_Connection=True;";
EndatixConfig.Configuration.MigrationsAssembly = "Endatix.Migrations";
// or
config.WithConnectionString("Server=.;Database=Endatix;Trusted_Connection=True;", "Endatix.Migrations");
```

**To**:
```csharp
// In your startup configuration
builder.Services.AddEndatix(configuration)
    .Persistence
        .UseSqlServer<AppDbContext>(options => 
            options.WithConnectionString("Server=.;Database=Endatix;Trusted_Connection=True;"))
    .Build();

// Or in appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=Endatix;Trusted_Connection=True;"
  }
}
```

### DefaultFormDefinitionJson

**From**:
```csharp
EndatixConfig.Configuration.DefaultFormDefinitionJson = "{\"logoPosition\": \"right\"}";
// or
config.UseDefaultFormDefinitionJson("{\"logoPosition\": \"right\"}");
```

**To**:
```csharp
// In your startup configuration
builder.Services.Configure<SubmissionOptions>(options => 
{
    options.DefaultFormDefinition = "{\"logoPosition\": \"right\"}";
});

// Or in appsettings.json
{
  "Endatix": {
    "Submissions": {
      "DefaultFormDefinition": "{\"logoPosition\": \"right\"}"
    }
  }
}
```

## Complete Migration Example

**Before**:
```csharp
// Using the legacy EndatixConfig
EndatixConfig.Configuration.UseSnowflakeIds = true;
EndatixConfig.Configuration.SnowflakeGeneratorId = 1;
EndatixConfig.Configuration.TablePrefix = "end_";
EndatixConfig.Configuration.ConnectionString = "Server=.;Database=Endatix;Trusted_Connection=True;";
EndatixConfig.Configuration.SeedSampleData = true;
EndatixConfig.Configuration.DefaultFormDefinitionJson = "{\"logoPosition\": \"right\"}";
```

**After**:
```csharp
// Using the builder pattern
builder.Services.AddEndatix(configuration)
    .Persistence
        .UseSqlServer<AppDbContext>(options => options
            .WithConnectionString("Server=.;Database=Endatix;Trusted_Connection=True;")
            .WithSnowflakeIds(1)
            .WithCustomTablePrefix("end_"))
        .EnableSampleDataSeeding()
    .Build();

// Configure other options
builder.Services.Configure<SubmissionOptions>(options => 
{
    options.DefaultFormDefinition = "{\"logoPosition\": \"right\"}";
});
```

**Or via appsettings.json**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=Endatix;Trusted_Connection=True;"
  },
  "Endatix": {
    "Data": {
      "SeedSampleData": true
    },
    "Submissions": {
      "DefaultFormDefinition": "{\"logoPosition\": \"right\"}"
    }
  }
}
```

## Timeline for Removal

The `EndatixConfig` class is marked as obsolete in the current version and will be removed in a future version. All new development should use the options pattern and builder methods described in this guide. 