using Endatix.Setup;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var endatixBuilder = builder.CreateEndatix()
                            .AddDefaultSetup()
                            .AddApiEndpoints();

var app = builder.Build();

app.UseEndatixMiddleware()
    .UseEndatixApi();

app.MapHealthChecks("/healthz");

app.Run();
