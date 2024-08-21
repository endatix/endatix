using Endatix.Setup;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

var endatixBuilder = builder.CreateEndatix()
                            .AddDefaultSetup()
                            .AddApiEndpoints();

var app = builder.Build();

app.UseEndatixMiddleware()
            .UseEndatixApi();

app.Run();

