using Endatix.Setup;

var builder = WebApplication.CreateBuilder(args);

// Instantiate the Endatix Platform
builder.CreateEndatix()
    .AddDefaultSetup()
    .AddApiEndpoints();

var app = builder.Build();

// Register the Endatix middleware
app.UseEndatixMiddleware()
            .UseEndatixApi();

app.Run();