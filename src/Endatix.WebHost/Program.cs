using Endatix.Hosting;
var builder = WebApplication.CreateBuilder(args);

// Add health checks
// builder.Services.AddHealthChecks();

// Configure Endatix with fluent API
builder.Services
    .AddEndatix(builder.Configuration)
    .UseDefaults();

var app = builder.Build();

// Configure middleware
app.UseEndatix();

// Map health checks
// app.MapHealthChecks("/healthz");

// Apply migrations and seed data
await app.Services.ApplyDbMigrationsAsync();
await app.SeedInitialUserAsync();

app.Run();
