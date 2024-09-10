using Endatix.Core.UseCases.Forms.List;
using Endatix.Setup;
using MediatR;

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