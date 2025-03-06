using Endatix.Hosting;
var builder = WebApplication.CreateBuilder(args);

// Add health checks
// builder.Services.AddHealthChecks();

// Configure Endatix with fluent API
builder.Services
    .AddEndatixWithDefaults(builder.Configuration);

var app = builder.Build();

app.UseEndatix();

// Map health checks
// app.MapHealthChecks("/healthz");

// Apply migrations and seed data
await app.Services.ApplyDbMigrationsAsync();
await app.SeedInitialUserAsync();

app.Run();
