using Endatix.Hosting;
using Endatix.Hosting.Builders;

var builder = WebApplication.CreateBuilder(args);

// Add health checks
// builder.Services.AddHealthChecks();

// Configure Endatix with fluent API
builder.Services.AddEndatix(builder.Configuration)
    .UseDefaults()
    .Api
        .AddSwagger()
        .AddVersioning()
        .Parent()
    .Persistence
        .EnableAutoMigrations()
        .ScanAssembliesForEntities(typeof(Program).Assembly);

var app = builder.Build();

// Configure middleware
app.UseEndatix();

// Map health checks
// app.MapHealthChecks("/healthz");

// Apply migrations and seed data
await app.Services.ApplyDbMigrationsAsync();
await app.SeedInitialUserAsync();

app.Run();
