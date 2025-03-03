using Endatix.Setup;

var builder = WebApplication.CreateBuilder(args);

// Instantiate the Endatix Platform
// builder.CreateEndatix()
//     .AddDefaultSetup()
//     .AddApiEndpoints();

var app = builder.Build();

// Register the Endatix middleware
// app.UseEndatixMiddleware()
//             .UseEndatixApi();

// TODO: Uncomment after Nuget package is updated
// await app.ApplyDbMigrationsAsync();
// await app.SeedInitialUserAsync();

app.Run();