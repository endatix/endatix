using Endatix.Setup;

var builder = WebApplication.CreateBuilder(args);

// builder.Services.AddHealthChecks();

builder.CreateEndatix()
    .AddDefaultSetup()
    .AddApiEndpoints();

var app = builder.Build();

app.UseEndatixMiddleware()
    .UseEndatixApi();

await app.ApplyDbMigrationsAsync();
await app.SeedInitialUserAsync();

// app.MapHealthChecks("/healthz");

app.Run();
