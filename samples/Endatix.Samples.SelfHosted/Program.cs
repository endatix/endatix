using Endatix.Core.UseCases.Forms.List;
using Endatix.Setup;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// Register the Endatix Platform
builder.CreateEndatix()
    .AddDefaultSetup()
    .AddApiEndpoints();

var app = builder.Build();

// Register the Endatix middleware
app.UseEndatixMiddleware()
            .UseEndatixApi();

var result = new ListFormsQuery(1, 10);

app.MapGet("/", () => "Hello World! " + result.ToString());

app.Run();