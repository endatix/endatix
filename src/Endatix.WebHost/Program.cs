using Endatix.Setup;
using Endatix.Api.Infrastructure;
using FastEndpoints;
using FastEndpoints.Swagger;
using Serilog;

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    var endatixBuilder = builder.UseEndatix()
                                .UseDefaultSetup()
                                .UseApiEndpoints();

    // TODO: expose custom plugin logic for plugin custom domain event handlers 
    // builder.Services.AddMediatRInfrastructure(options =>
    //                 {
    //                     options.UsePipelineLogging();
    //                     options.AdditionalAssemblies =
    //                         [
    //                             Endatix.Samples.CustomEventHandlers.AssemblyReference.Assembly
    //                         ];
    //                 });

    var app = builder.Build();

    app.UseEndatixApi();

    app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "An unhandled exception occurred during bootstrapping");
}
finally
{
    Log.CloseAndFlush();
}